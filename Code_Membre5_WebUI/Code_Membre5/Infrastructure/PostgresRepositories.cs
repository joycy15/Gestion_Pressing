using Pressing.DAL;
using Pressing.Membre5.Demo.Application;
using D = Pressing.Membre5.Demo.Domain;
using M = Pressing.Models;

namespace Pressing.Membre5.Demo.Infrastructure;

// =====================================================================
//  Adaptateurs PostgreSQL : branchent Member5Service (inchangé) sur la
//  vraie base, en passant par les dépôts du projet Pressing.DAL.
//  Aucune requête SQL métier ici : on réutilise Pressing.DAL, et les
//  règles (statuts, historique, notifications, dates) restent celles
//  des déclencheurs de la base.
// =====================================================================

internal static class Convertisseurs
{
    public static DateTimeOffset Dt(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Local));
    public static DateTimeOffset? Dt(DateTime? d) => d is null ? null : Dt(d.Value);

    public static D.ModePaiement Vers(M.ModePaiement m) =>
        m == M.ModePaiement.MobileMoney ? D.ModePaiement.MtnMobileMoney : Enum.Parse<D.ModePaiement>(m.ToString());
    public static M.ModePaiement Vers(D.ModePaiement m) =>
        m == D.ModePaiement.MtnMobileMoney ? M.ModePaiement.MobileMoney : Enum.Parse<M.ModePaiement>(m.ToString());

    public static D.TypeEmail Vers(M.TypeEmail t) =>
        t == M.TypeEmail.Pret ? D.TypeEmail.CommandePrete : D.TypeEmail.AvertissementVente;
    public static M.TypeEmail Vers(D.TypeEmail t) =>
        t == D.TypeEmail.CommandePrete ? M.TypeEmail.Pret : M.TypeEmail.Avertissement;

    public static D.Commande Vers(M.Commande c)
    {
        if (c.Lignes.Count == 0) c.Lignes = CommandeRepository.LireLignes(c.IdCommande);
        return new D.Commande
        {
            Id = c.IdCommande,
            NumeroTicket = c.NumeroTicket,
            Client = new D.Client { Id = c.IdClient, NomComplet = c.NomClient, Telephone = c.TelephoneClient, Email = c.EmailClient },
            Service = c.NomService,
            Articles = c.Lignes.Select(l => l.ToString()).ToList(),
            MontantTotal = c.MontantTotal,
            Statut = Enum.Parse<D.StatutCommande>(c.Statut.ToString()),
            DateDepot = Dt(c.DateDepot),
            DatePrete = Dt(c.DatePrete),
            DateAvertissement = Dt(c.DateAvertissement),
            DateRetrait = Dt(c.DateRetrait),
            DateVente = Dt(c.DateVente),
            PrixVente = c.PrixVente
        };
    }
}

public sealed class PostgresCommandeRepository : ICommandeRepository
{
    public D.Commande? ParId(int id) { var c = CommandeRepository.ParId(id); return c is null ? null : Convertisseurs.Vers(c); }

    public D.Commande? TrouverParTicket(string ticket) { var c = CommandeRepository.ParTicket(ticket); return c is null ? null : Convertisseurs.Vers(c); }

    public IReadOnlyList<D.Commande> Rechercher(string terme)
    {
        if (string.IsNullOrWhiteSpace(terme)) return new List<D.Commande>();
        return CommandeRepository.Rechercher(terme).Select(c => Convertisseurs.Vers(c)).ToList();
    }

    // Commandes « Prête » depuis assez longtemps (délai lu dans la table parametre par la vue SQL).
    public IReadOnlyList<D.Commande> ListerPretesNonRetirees() =>
        CommandeRepository.AAvertir().Select(c => Convertisseurs.Vers(c)).ToList();

    // Commandes averties dont le délai de grâce est dépassé (vue SQL vue_commandes_a_vendre).
    public IReadOnlyList<D.Commande> ListerVentesEligibles(DateTimeOffset maintenant, int joursGrace) =>
        CommandeRepository.AVendre().Select(c => Convertisseurs.Vers(c)).ToList();

    // Le service modifie le statut en mémoire : on le transmet à la base.
    // Les dates, l'historique, les notifications et le contrôle « tout est payé » sont faits par les déclencheurs.
    public void Enregistrer(D.Commande commande)
    {
        var actuel = CommandeRepository.ParId(commande.Id) ?? throw new D.RegleMetierException("Commande introuvable en base.");
        var nouveau = Enum.Parse<M.StatutCommande>(commande.Statut.ToString());
        if (nouveau == actuel.Statut) return;
        if (nouveau == M.StatutCommande.Vendue) CommandeRepository.Vendre(commande.Id, (int)(commande.PrixVente ?? 0m));
        else CommandeRepository.ChangerStatut(commande.Id, nouveau);
    }
}

public sealed class PostgresPaiementRepository : IPaiementRepository
{
    public decimal TotalPaye(int commandeId) =>
        Convert.ToDecimal(Database.Scalaire("SELECT COALESCE(SUM(montant), 0) FROM paiement WHERE id_commande = @id", Database.P("@id", commandeId)));

    public IReadOnlyList<D.Paiement> ListerPourCommande(int commandeId) =>
        PaiementRepository.ListerParCommande(commandeId).Select(p => new D.Paiement
        {
            Id = p.IdPaiement, CommandeId = p.IdCommande, EmployeId = p.IdUtilisateur, Montant = p.Montant,
            Mode = Convertisseurs.Vers(p.ModePaiement), Moment = Enum.Parse<D.MomentPaiement>(p.Moment.ToString()),
            Date = Convertisseurs.Dt(p.DatePaiement)
        }).ToList();

    public void Ajouter(D.Paiement p)
    {
        // Le prix d'une vente est enregistré dans commande.prix_vente (voir Enregistrer) :
        // la table paiement n'accepte que Depot, Retrait et Livraison.
        if (p.Moment == D.MomentPaiement.Vente) return;
        if (p.Montant != decimal.Truncate(p.Montant)) throw new D.RegleMetierException("Le montant doit être un nombre entier de FCFA.");
        PaiementRepository.Ajouter(new M.Paiement
        {
            IdCommande = p.CommandeId, IdUtilisateur = p.EmployeId, Montant = (int)p.Montant,
            ModePaiement = Convertisseurs.Vers(p.Mode), Moment = Enum.Parse<M.MomentPaiement>(p.Moment.ToString())
        });
    }
}

public sealed class PostgresEmailJournalRepository : IEmailJournalRepository
{
    public bool ExisteAutomatique(int commandeId, D.TypeEmail type) =>
        (bool)Database.Scalaire(
            "SELECT EXISTS (SELECT 1 FROM email_envoye WHERE id_commande = @id AND type = @t AND manuel = FALSE AND resultat = 'Envoye')",
            Database.P("@id", commandeId), Database.P("@t", Convertisseurs.Vers(type).ToString()))!;

    public void Ajouter(D.EmailJournal e) =>
        EmailRepository.Enregistrer(new M.EmailEnvoye
        {
            IdCommande = e.CommandeId, Type = Convertisseurs.Vers(e.Type), Destinataire = e.Destinataire,
            Resultat = e.Envoye ? M.ResultatEmail.Envoye : M.ResultatEmail.Echec, Erreur = e.Erreur, Manuel = e.Manuel
        });

    public IReadOnlyList<D.EmailJournal> Lister() =>
        EmailRepository.ListerRecents().Select(e => new D.EmailJournal
        {
            Id = e.IdEmail, CommandeId = e.IdCommande, NumeroTicket = e.NumeroTicket, Type = Convertisseurs.Vers(e.Type),
            Destinataire = e.Destinataire, Date = Convertisseurs.Dt(e.DateEnvoi), Envoye = e.Resultat == M.ResultatEmail.Envoye,
            Erreur = e.Erreur, Manuel = e.Manuel
        }).ToList();
}

public sealed class PostgresNotificationRepository : INotificationRepository
{
    // Les notifications sont créées par la base (déclencheurs) : un email en échec crée déjà
    // une notification « EmailEchec » pour l'administrateur. Rien à écrire ici.
    public void Ajouter(D.Notification notification) { }

    public IReadOnlyList<D.Notification> ListerNonTraitees() =>
        NotificationRepository.ListerNonTraitees(M.RoleUtilisateur.Administrateur).Select(n => new D.Notification
        {
            Id = n.IdNotification, CommandeId = n.IdCommande, Titre = Libelle(n), Message = Message(n),
            Date = Convertisseurs.Dt(n.DateCreation), Traitee = n.Traitee
        }).ToList();

    public void MarquerTraitee(int notificationId) => NotificationRepository.MarquerTraitee(notificationId);

    private static string Libelle(M.Notification n) => M.Libelles.De(n.Type);

    private static string Message(M.Notification n) => n.Type switch
    {
        M.TypeNotification.EmailEchec => $"L'email du ticket {n.NumeroTicket} n'est pas parti. Appelez {n.NomClient} au {n.TelephoneClient} ou renvoyez-le depuis le journal.",
        M.TypeNotification.AVendre => $"Le ticket {n.NumeroTicket} ({n.NomClient}) a dépassé le délai de grâce : il peut être vendu.",
        _ => $"{n.NumeroTicket} · {n.NomService} ({n.NomClient})"
    };
}

// L'historique des statuts est écrit automatiquement par la base (déclencheur trg_commande_apres),
// avec l'utilisateur transmis par Database.IdUtilisateurConnecte.
public sealed class PostgresHistoriqueRepository : IHistoriqueStatutRepository
{
    public void Ajouter(D.HistoriqueStatut entree) { }
}
