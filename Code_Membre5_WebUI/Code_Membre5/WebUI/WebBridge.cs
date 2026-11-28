using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Pressing.DAL;
using Pressing.Membre5.Demo.Application;
using Pressing.Membre5.Demo.Infrastructure;
using D = Pressing.Membre5.Demo.Domain;
using M = Pressing.Models;

namespace Pressing.Membre5.Demo.WebUI;

// Pont entre l'interface HTML/CSS/JS et Member5Service, branché sur PostgreSQL.
// Aucune règle métier ici : chaque appel est transmis au service existant ; les erreurs de la base
// (déclencheurs PRxxx) sont traduites en messages lisibles par Pressing.DAL.ErreurBase.
internal sealed class WebBridge(Member5Service service, PostgresCommandeRepository commandes, OptionsPressing options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private D.EmployeCourant? _employe;
    private M.Utilisateur? _utilisateur;

    public async Task<string> HandleAsync(string message)
    {
        int id = 0;
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            id = root.GetProperty("id").GetInt32();
            var method = root.GetProperty("method").GetString() ?? "";
            var args = root.TryGetProperty("args", out var a) ? a : default;
            var result = await DispatchAsync(method, args);
            return JsonSerializer.Serialize(new { id, ok = true, result }, Json);
        }
        catch (D.RegleMetierException ex) { return JsonSerializer.Serialize(new { id, ok = false, error = ex.Message }, Json); }
        catch (Exception ex) { return JsonSerializer.Serialize(new { id, ok = false, error = ErreurBase.Message(ex) }, Json); }
    }

    private async Task<object?> DispatchAsync(string method, JsonElement a)
    {
        switch (method)
        {
            case "session":
                return Session();

            case "login":
                _utilisateur = Authentification.Connecter(a.GetProperty("login").GetString(), a.GetProperty("motDePasse").GetString());
                _employe = new D.EmployeCourant { Id = _utilisateur.IdUtilisateur, Role = Enum.Parse<D.RoleEmploye>(_utilisateur.Role.ToString()) };
                Database.IdUtilisateurConnecte = _utilisateur.IdUtilisateur;   // renseigne l'historique des statuts (RG9)
                ChargerOptions();
                return Utilisateur();

            case "logout":
                _employe = null; _utilisateur = null; Database.IdUtilisateurConnecte = null;
                return null;
        }

        var employe = _employe ?? throw new D.RegleMetierException("Veuillez vous connecter.");
        bool admin = employe.Role == D.RoleEmploye.Administrateur;
        bool retrait = admin || employe.Role == D.RoleEmploye.Caissier;

        switch (method)
        {
            case "state":
                var term = a.GetProperty("term").GetString() ?? "";
                return new
                {
                    recherche = retrait ? service.Rechercher(term).Select(Commande).ToList() : new List<object>(),
                    eligibles = admin ? service.ListerVentesEligibles().Select(Commande).ToList() : new List<object>(),
                    notifications = admin ? service.Notifications().Select(n => (object)new { n.Id, n.Titre, n.Message, Date = Ms(n.Date) }).ToList() : new List<object>(),
                    emails = admin ? service.JournalEmails().Select(e => (object)new { e.Id, Ticket = e.NumeroTicket, e.Type, e.Destinataire, Date = Ms(e.Date), e.Envoye, e.Erreur, e.Manuel }).ToList() : new List<object>()
                };

            case "payer":
                service.EnregistrerPaiementRetrait(Trouver(a), a.GetProperty("montant").GetDecimal(), Enum.Parse<D.ModePaiement>(a.GetProperty("mode").GetString()!), employe);
                return null;

            case "retirer":
                service.ValiderRetrait(Trouver(a), employe);
                return null;

            case "vendre":
                service.MarquerVendue(Trouver(a), a.GetProperty("prix").GetDecimal(), Enum.Parse<D.ModePaiement>(a.GetProperty("mode").GetString()!), employe);
                return null;

            case "traiter":
                service.MarquerNotificationTraitee(a.GetProperty("id").GetInt32());
                return null;

            case "renvoyer":
                var commande = commandes.TrouverParTicket(a.GetProperty("ticket").GetString() ?? "") ?? throw new D.RegleMetierException("La commande associée n'a pas été retrouvée.");
                await service.RenvoyerEmailManuellementAsync(commande, Enum.Parse<D.TypeEmail>(a.GetProperty("type").GetString()!), employe);
                return null;

            case "sync":
                if (retrait) await SynchroniserAsync();
                return null;

            default:
                throw new D.RegleMetierException("Action inconnue : " + method);
        }
    }

    // Remplace l'ancienne « simulation » : traitements réels déclenchés au démarrage, toutes les heures et par le bouton Synchroniser.
    private async Task SynchroniserAsync()
    {
        ChargerOptions();
        NotificationRepository.GenererAVendre();            // notifications « À vendre » (RG15)
        await service.VerifierAvertissementsAsync();        // Prête depuis 30 jours -> Avertie + email (RG14)

        // Commandes passées à « Prête » (par le repasseur) dont l'email n'est jamais parti : envoi automatique unique (RG16).
        // Un email déjà tenté (même en échec) n'est pas relancé tout seul : l'administrateur le renvoie depuis le journal.
        foreach (var c in CommandeRepository.PretesSansEmail())
        {
            if (EmailRepository.ListerParCommande(c.IdCommande).Any(e => e.Type == M.TypeEmail.Pret && !e.Manuel)) continue;
            var cmd = commandes.ParId(c.IdCommande);
            if (cmd is not null) await service.SignalerCommandePreteAsync(cmd);
        }
    }

    // Délais et coordonnées du pressing : lus dans la table « parametre ».
    private void ChargerOptions()
    {
        options.JoursAvantAvertissement = ParametreRepository.LireEntier(M.Parametre.DelaiAvertissementJours, 30);
        options.JoursGraceAvantVente = ParametreRepository.LireEntier(M.Parametre.DelaiGraceJours, 7);
        options.NomPressing = ParametreRepository.Lire(M.Parametre.NomPressing) ?? options.NomPressing;
        options.AdressePressing = ParametreRepository.Lire(M.Parametre.AdressePressing) ?? options.AdressePressing;
        options.TelephonePressing = ParametreRepository.Lire(M.Parametre.TelephonePressing) ?? options.TelephonePressing;
        options.Horaires = ParametreRepository.Lire(M.Parametre.HorairesPressing) ?? options.Horaires;
    }

    private object Session()
    {
        try
        {
            string nomBase = new NpgsqlConnectionStringBuilder(Configuration.ChaineConnexion).Database ?? "";
            if (!Database.TesterConnexion(out string erreur)) return new { user = (object?)null, @base = nomBase, erreur };
            return new { user = Utilisateur(), @base = nomBase, erreur = "" };
        }
        catch (Exception ex) { return new { user = (object?)null, @base = "", erreur = ErreurBase.Message(ex) }; }
    }

    private object? Utilisateur() =>
        _utilisateur is null ? null : new { id = _utilisateur.IdUtilisateur, nom = _utilisateur.Nom, role = _utilisateur.Role.ToString() };

    private D.Commande Trouver(JsonElement a) =>
        commandes.ParId(a.GetProperty("id").GetInt32()) ?? throw new D.RegleMetierException("Commande introuvable.");

    private object Commande(D.Commande c) => new
    {
        c.Id, Ticket = c.NumeroTicket, Nom = c.Client.NomComplet, Tel = c.Client.Telephone, Mail = c.Client.Email ?? "",
        c.Service, c.Articles, Total = c.MontantTotal, Statut = c.Statut,
        Paye = c.MontantTotal - service.ResteAPayer(c), Reste = service.ResteAPayer(c),
        Prete = Ms(c.DatePrete), Avert = Ms(c.DateAvertissement), Vendable = Ms(c.DateAvertissement?.AddDays(options.JoursGraceAvantVente))
    };

    private static long? Ms(DateTimeOffset? d) => d?.ToUnixTimeMilliseconds();
}
