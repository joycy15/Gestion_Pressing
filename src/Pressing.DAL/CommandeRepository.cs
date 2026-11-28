// =====================================================================
//  CommandeRepository.cs  —  Requêtes sur les commandes (le cœur)
// ---------------------------------------------------------------------
//  Tables : commande + ligne_commande (+ paiement au dépôt / au retrait).
//  Vues   : vue_commande_solde (commande + client + service + payé),
//           vue_file_lavage, vue_file_repassage, vue_file_livraison,
//           vue_commandes_a_avertir, vue_commandes_a_vendre.
//
//  CE QUE LA BASE FAIT TOUTE SEULE (triggers), à ne pas refaire en C# :
//    - numéro de ticket PR-AAAA-000001 ;
//    - « repassage seul » -> statut EnRepassage dès la création ;
//    - date de retrait prévue = dépôt + délai du service ;
//    - livraison : adresse du client et frais (paramètre FraisLivraison)
//      si rien n'est saisi ;
//    - montant total = somme des lignes + frais de livraison ; statut de paiement ;
//    - dates prête / avertie / retrait / livraison / vente ;
//    - refus des statuts interdits (PR005), du retrait ou de la livraison
//      non payés (PR003), d'une livraison sans adresse (PR007)... ;
//    - historique des statuts et notifications laveur / repasseur / livreur.
//
//  ORDRE IMPORTANT au dépôt : commande -> lignes -> paiement.
//  (Si on paie avant d'avoir mis les lignes, le total vaut 0 et la base
//  refuse le paiement : PR001.)
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès aux commandes, à leurs lignes et aux files de travail.</summary>
public static class CommandeRepository
{
    // Colonnes communes à vue_commande_solde et aux vues qui en dérivent
    private const string Colonnes =
        "id_commande, numero_ticket, id_client, nom_client, telephone, email, id_service, service, " +
        "prioritaire, id_utilisateur, mode_recuperation, adresse_livraison, frais_livraison, " +
        "date_depot, date_retrait_prevue, date_prete, date_avertissement, date_retrait, date_vente, " +
        "date_livraison, prix_vente, statut, statut_paiement, montant_total, montant_paye";

    // Construit « SELECT <colonnes> FROM <source> »
    private static string Select(string source) => $"SELECT {Colonnes} FROM {source} ";

    // Tri des files de l'atelier : express d'abord, puis le plus ancien dépôt (RG8)
    private const string TriFile = "ORDER BY prioritaire DESC, date_depot";


    // =====================================================================
    //  CRÉATION (écran Nouveau dépôt)
    // =====================================================================

    /// <summary>
    /// Enregistre une nouvelle commande en UNE transaction :
    ///   1. la commande ; 2. ses lignes ; 3. l'avance éventuelle (moment Dépôt).
    /// Si une étape échoue, RIEN n'est enregistré.
    /// Remplit ensuite IdCommande, NumeroTicket, Statut, dates et MontantTotal
    /// dans l'objet « commande » reçu, et renvoie le numéro de ticket.
    /// </summary>
    /// <param name="commande">IdClient, IdService, IdUtilisateur, ModeRecuperation et Lignes remplis.
    /// DateRetraitPrevue : laisser vide pour le délai normal du service.
    /// AdresseLivraison / FraisLivraison : laisser vides pour l'adresse du client et les frais du paramètre.</param>
    /// <param name="avance">Paiement au dépôt, ou null si le client paiera au retrait.</param>
    public static string Creer(Commande commande, Paiement? avance)
    {
        if (commande.Lignes.Count == 0)
            throw new InvalidOperationException("Une commande doit contenir au moins un vêtement.");

        return Database.EnTransaction((connexion, transaction) =>
        {
            // 1. La commande. date_retrait_prevue NULL -> la base la calcule.
            object? dateRetrait = commande.DateRetraitPrevue == default
                ? null
                : commande.DateRetraitPrevue.Date;

            // Adresse vide -> NULL : la base prendra l'adresse du client
            string? adresse = string.IsNullOrWhiteSpace(commande.AdresseLivraison)
                ? null
                : commande.AdresseLivraison.Trim();

            var infos = Database.LireListe(connexion, transaction,
                "INSERT INTO commande (id_client, id_service, id_utilisateur, date_retrait_prevue, " +
                "                      mode_recuperation, adresse_livraison, frais_livraison) " +
                "VALUES (@client, @service, @user, @retrait, @mode, @adresse, @frais) " +
                "RETURNING id_commande, numero_ticket, statut, date_depot, date_retrait_prevue, " +
                "          adresse_livraison, frais_livraison",
                r => (Id: r.Int("id_commande"),
                      Ticket: r.Texte("numero_ticket"),
                      Statut: r.EnumDe<StatutCommande>("statut"),
                      Depot: r.Date("date_depot"),
                      Retrait: r.Date("date_retrait_prevue"),
                      Adresse: r.TexteOuNull("adresse_livraison"),
                      Frais: r.Int("frais_livraison")),
                Database.P("@client", commande.IdClient),
                Database.P("@service", commande.IdService),
                Database.P("@user", commande.IdUtilisateur),
                Database.P("@retrait", dateRetrait),
                Database.P("@mode", commande.ModeRecuperation.ToString()),
                Database.P("@adresse", adresse),
                Database.P("@frais", commande.FraisLivraison))[0];   // null -> paramètre FraisLivraison

            // 2. Les lignes (le prix unitaire est COPIÉ de la grille au moment du dépôt)
            foreach (LigneCommande ligne in commande.Lignes)
            {
                ligne.IdLigne = Convert.ToInt32(Database.Scalaire(connexion, transaction,
                    "INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) " +
                    "VALUES (@cmd, @type, @qte, @prix, @obs) RETURNING id_ligne",
                    Database.P("@cmd", infos.Id),
                    Database.P("@type", ligne.IdTypeVetement),
                    Database.P("@qte", ligne.Quantite),
                    Database.P("@prix", ligne.PrixUnitaire),
                    Database.P("@obs", string.IsNullOrWhiteSpace(ligne.Observation) ? null : ligne.Observation.Trim())));
                ligne.IdCommande = infos.Id;
            }

            // 3. L'avance, APRÈS les lignes (le total est maintenant connu)
            if (avance is not null && avance.Montant > 0)
            {
                avance.IdCommande = infos.Id;
                avance.Moment = MomentPaiement.Depot;
                avance.IdPaiement = PaiementRepository.Ajouter(connexion, transaction, avance);
            }

            // On complète l'objet avec ce que la base a calculé
            commande.IdCommande        = infos.Id;
            commande.NumeroTicket      = infos.Ticket;
            commande.Statut            = infos.Statut;
            commande.DateDepot         = infos.Depot;
            commande.DateRetraitPrevue = infos.Retrait;
            commande.AdresseLivraison  = infos.Adresse;
            commande.FraisLivraison    = infos.Frais;
            commande.MontantTotal      = Convert.ToInt32(Database.Scalaire(connexion, transaction,
                "SELECT montant_total FROM commande WHERE id_commande = @id", Database.P("@id", infos.Id)));
            commande.MontantPaye       = avance?.Montant ?? 0;

            return infos.Ticket;
        });
    }


    // =====================================================================
    //  LECTURE
    // =====================================================================

    /// <summary>Une commande COMPLÈTE (avec lignes et paiements) par son id, ou null.</summary>
    public static Commande? ParId(int idCommande) =>
        Completer(Database.LireUn(Select("vue_commande_solde") + "WHERE id_commande = @id",
            VersCommande, Database.P("@id", idCommande)));

    /// <summary>Une commande COMPLÈTE par son numéro de ticket (ex. « pr-2026-000004 » accepté).</summary>
    public static Commande? ParTicket(string numeroTicket) =>
        Completer(Database.LireUn(Select("vue_commande_solde") + "WHERE numero_ticket = @n",
            VersCommande, Database.P("@n", numeroTicket.Trim().ToUpperInvariant())));

    /// <summary>
    /// Recherche par ticket, nom ou téléphone du client, avec filtre de statut
    /// facultatif (null = tous). Les plus récentes d'abord, 200 maximum.
    /// Les lignes et paiements ne sont PAS chargés (liste rapide) : utiliser ParId pour le détail.
    /// </summary>
    public static List<Commande> Rechercher(string? texte, StatutCommande? statut = null)
    {
        string sql = Select("vue_commande_solde") +
                     "WHERE (numero_ticket ILIKE @t OR nom_client ILIKE @t OR telephone ILIKE @t) ";

        var parametres = new List<NpgsqlParameter> { Database.P("@t", "%" + (texte ?? "").Trim() + "%") };

        if (statut is not null)
        {
            sql += "AND statut = @statut ";
            parametres.Add(Database.P("@statut", statut.Value.ToString()));
        }

        sql += "ORDER BY date_depot DESC LIMIT 200";
        return Database.LireListe(sql, VersCommande, parametres.ToArray());
    }

    /// <summary>Toutes les commandes d'un client, les plus récentes d'abord.</summary>
    public static List<Commande> ListerParClient(int idClient) =>
        Database.LireListe(Select("vue_commande_solde") + "WHERE id_client = @id ORDER BY date_depot DESC",
            VersCommande, Database.P("@id", idClient));

    /// <summary>Lignes (vêtements) d'une commande, avec le libellé du vêtement.</summary>
    public static List<LigneCommande> LireLignes(int idCommande) =>
        Database.LireListe(
            "SELECT l.id_ligne, l.id_commande, l.id_type_vetement, l.quantite, l.prix_unitaire, " +
            "l.observation, tv.libelle " +
            "FROM ligne_commande l JOIN type_vetement tv ON tv.id_type_vetement = l.id_type_vetement " +
            "WHERE l.id_commande = @id ORDER BY l.id_ligne",
            VersLigne, Database.P("@id", idCommande));


    // ------------------------- Files de travail -------------------------

    /// <summary>File du LAVEUR : Déposée et En lavage, express d'abord.</summary>
    public static List<Commande> FileLavage() =>
        ChargerLignes(Database.LireListe(Select("vue_file_lavage") + TriFile, VersCommande));

    /// <summary>File du REPASSEUR : En repassage (lavées + repassage seul), express d'abord.</summary>
    public static List<Commande> FileRepassage() =>
        ChargerLignes(Database.LireListe(Select("vue_file_repassage") + TriFile, VersCommande));

    /// <summary>
    /// File du LIVREUR : toutes les commandes « à livrer » non terminées, dès le dépôt.
    /// D'abord celles en route, puis les prêtes (express d'abord), puis celles encore à l'atelier.
    /// </summary>
    public static List<Commande> FileLivraison() =>
        ChargerLignes(Database.LireListe(
            Select("vue_file_livraison") +
            "ORDER BY CASE statut WHEN 'EnLivraison' THEN 0 WHEN 'Prete' THEN 1 WHEN 'Avertie' THEN 1 ELSE 2 END, " +
            "prioritaire DESC, date_retrait_prevue, date_depot",
            VersCommande));

    /// <summary>Prêtes depuis plus de DelaiAvertissementJours : email d'avertissement à envoyer (RG14).</summary>
    public static List<Commande> AAvertir() =>
        Database.LireListe(Select("vue_commandes_a_avertir") + "ORDER BY date_prete", VersCommande);

    /// <summary>Averties depuis plus de DelaiGraceJours : l'administrateur peut les vendre (RG15).</summary>
    public static List<Commande> AVendre() =>
        ChargerLignes(Database.LireListe(Select("vue_commandes_a_vendre") + "ORDER BY date_avertissement", VersCommande));

    /// <summary>
    /// Commandes Prêtes dont l'email « commande prête » n'est jamais parti avec succès.
    /// La BLL les renvoie au démarrage (ex. Internet coupé au moment du passage à Prête).
    /// </summary>
    public static List<Commande> PretesSansEmail() =>
        Database.LireListe(
            Select("vue_commande_solde v") +
            "WHERE v.statut = 'Prete' AND NOT EXISTS (" +
            "  SELECT 1 FROM email_envoye e " +
            "  WHERE e.id_commande = v.id_commande AND e.type = 'Pret' AND e.resultat = 'Envoye') " +
            "ORDER BY v.date_prete",
            VersCommande);


    // =====================================================================
    //  CHANGEMENTS DE STATUT
    // =====================================================================

    /// <summary>
    /// Passe la commande à un nouveau statut (EnLavage, EnRepassage, Prete, Avertie...).
    /// La base vérifie que le passage est autorisé (sinon erreur PR005),
    /// remplit les dates, l'historique et les notifications.
    /// Renvoie false si la commande n'existe pas.
    /// </summary>
    public static bool ChangerStatut(int idCommande, StatutCommande nouveauStatut) =>
        Database.Executer(
            "UPDATE commande SET statut = @statut WHERE id_commande = @id",
            Database.P("@statut", nouveauStatut.ToString()),
            Database.P("@id", idCommande)) > 0;

    /// <summary>
    /// Retrait par le client, en UNE transaction :
    ///   1. le paiement du reste (si « solde » n'est pas null) ;
    ///   2. le passage au statut Retirée.
    /// S'il reste quelque chose à payer, la base refuse (PR003) et le
    /// paiement n'est pas enregistré non plus.
    /// </summary>
    public static void Retirer(int idCommande, Paiement? solde) =>
        Database.EnTransaction((connexion, transaction) =>
        {
            if (solde is not null && solde.Montant > 0)
            {
                solde.IdCommande = idCommande;
                solde.Moment = MomentPaiement.Retrait;
                solde.IdPaiement = PaiementRepository.Ajouter(connexion, transaction, solde);
            }

            int nb = Database.Executer(connexion, transaction,
                "UPDATE commande SET statut = 'Retiree' WHERE id_commande = @id",
                Database.P("@id", idCommande));

            if (nb == 0)
                throw new InvalidOperationException("Commande introuvable.");
        });

    // ------------------------- Livraison -------------------------

    /// <summary>Le livreur part avec la commande (Prête -> En livraison). Refusé si « à récupérer » (PR008).</summary>
    public static bool PartirEnLivraison(int idCommande) =>
        ChangerStatut(idCommande, StatutCommande.EnLivraison);

    /// <summary>
    /// Remise au client par le livreur, en UNE transaction :
    ///   1. le paiement du reste au livreur (si « solde » n'est pas null) ;
    ///   2. le passage au statut Livrée.
    /// S'il reste quelque chose à payer, la base refuse (PR003) et rien n'est enregistré.
    /// </summary>
    public static void Livrer(int idCommande, Paiement? solde) =>
        Database.EnTransaction((connexion, transaction) =>
        {
            if (solde is not null && solde.Montant > 0)
            {
                solde.IdCommande = idCommande;
                solde.Moment = MomentPaiement.Livraison;
                solde.IdPaiement = PaiementRepository.Ajouter(connexion, transaction, solde);
            }

            int nb = Database.Executer(connexion, transaction,
                "UPDATE commande SET statut = 'Livree' WHERE id_commande = @id",
                Database.P("@id", idCommande));

            if (nb == 0)
                throw new InvalidOperationException("Commande introuvable.");
        });

    /// <summary>Échec de livraison (client absent...) : la commande revient « Prête » au pressing.</summary>
    public static bool EchecLivraison(int idCommande) =>
        ChangerStatut(idCommande, StatutCommande.Prete);

    /// <summary>
    /// Le client change d'avis après le dépôt (retrait <-> livraison, autre adresse, autres frais).
    /// Possible tant que la commande n'est pas partie en livraison ni terminée (sinon PR009).
    /// adresse null -> adresse du client ; frais null -> frais actuels, ou paramètre si on passe en livraison.
    /// La base recalcule le total et refuse si le client a déjà payé plus que le nouveau total (PR001).
    /// </summary>
    public static bool ModifierLivraison(int idCommande, ModeRecuperation mode, string? adresse, int? frais) =>
        Database.Executer(
            "UPDATE commande SET mode_recuperation = @mode, adresse_livraison = @adresse, " +
            "frais_livraison = COALESCE(@frais, frais_livraison) WHERE id_commande = @id",
            Database.P("@mode", mode.ToString()),
            Database.P("@adresse", string.IsNullOrWhiteSpace(adresse) ? null : adresse.Trim()),
            Database.P("@frais", frais),
            Database.P("@id", idCommande)) > 0;

    // ------------------------- Fin de vie -------------------------

    /// <summary>Vente d'une commande non récupérée (Avertie -> Vendue), avec le prix obtenu.</summary>
    public static bool Vendre(int idCommande, int prixVente) =>
        Database.Executer(
            "UPDATE commande SET statut = 'Vendue', prix_vente = @prix WHERE id_commande = @id",
            Database.P("@prix", prixVente),
            Database.P("@id", idCommande)) > 0;

    /// <summary>Annulation par l'administrateur.</summary>
    public static bool Annuler(int idCommande) => ChangerStatut(idCommande, StatutCommande.Annulee);


    // =====================================================================
    //  Ligne -> objet
    // =====================================================================

    /// <summary>Ajoute lignes et paiements à une commande trouvée (ou renvoie null).</summary>
    private static Commande? Completer(Commande? commande)
    {
        if (commande is null)
            return null;

        commande.Lignes = LireLignes(commande.IdCommande);
        commande.Paiements = PaiementRepository.ListerParCommande(commande.IdCommande);
        return commande;
    }

    /// <summary>Charge les lignes de chaque commande (le laveur doit voir les vêtements).</summary>
    private static List<Commande> ChargerLignes(List<Commande> commandes)
    {
        foreach (Commande c in commandes)
            c.Lignes = LireLignes(c.IdCommande);
        return commandes;
    }

    /// <summary>Transforme une ligne de vue_commande_solde (ou d'une vue dérivée) en Commande.</summary>
    private static Commande VersCommande(NpgsqlDataReader r) => new Commande
    {
        IdCommande         = r.Int("id_commande"),
        NumeroTicket       = r.Texte("numero_ticket"),
        IdClient           = r.Int("id_client"),
        NomClient          = r.Texte("nom_client"),
        TelephoneClient    = r.Texte("telephone"),
        EmailClient        = r.Texte("email"),
        IdService          = r.Int("id_service"),
        NomService         = r.Texte("service"),
        Prioritaire        = r.Bool("prioritaire"),
        IdUtilisateur      = r.Int("id_utilisateur"),
        ModeRecuperation   = r.EnumDe<ModeRecuperation>("mode_recuperation"),
        AdresseLivraison   = r.TexteOuNull("adresse_livraison"),
        FraisLivraison     = r.Int("frais_livraison"),
        DateDepot          = r.Date("date_depot"),
        DateRetraitPrevue  = r.Date("date_retrait_prevue"),
        DatePrete          = r.DateOuNull("date_prete"),
        DateAvertissement  = r.DateOuNull("date_avertissement"),
        DateRetrait        = r.DateOuNull("date_retrait"),
        DateVente          = r.DateOuNull("date_vente"),
        DateLivraison      = r.DateOuNull("date_livraison"),
        PrixVente          = r.IntOuNull("prix_vente"),
        Statut             = r.EnumDe<StatutCommande>("statut"),
        StatutPaiement     = r.EnumDe<StatutPaiement>("statut_paiement"),
        MontantTotal       = r.Int("montant_total"),
        MontantPaye        = r.Int("montant_paye")
    };

    /// <summary>Transforme une ligne de ligne_commande en LigneCommande.</summary>
    private static LigneCommande VersLigne(NpgsqlDataReader r) => new LigneCommande
    {
        IdLigne         = r.Int("id_ligne"),
        IdCommande      = r.Int("id_commande"),
        IdTypeVetement  = r.Int("id_type_vetement"),
        Quantite        = r.Int("quantite"),
        PrixUnitaire    = r.Int("prix_unitaire"),
        Observation     = r.TexteOuNull("observation"),
        LibelleVetement = r.Texte("libelle")
    };
}
