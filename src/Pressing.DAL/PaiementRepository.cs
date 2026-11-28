// =====================================================================
//  PaiementRepository.cs  —  Requêtes sur la table paiement
// ---------------------------------------------------------------------
//  Les paiements sont seulement NOTÉS (espèces, Orange Money, MTN Mobile
//  Money), au dépôt et/ou au retrait.
//
//  Ce que fait la base toute seule à chaque paiement :
//    - refuse si le total payé dépasse le montant de la commande (PR001) ;
//    - recalcule le statut de paiement (NonPaye / PartiellementPaye / Paye).
//
//  Le paiement au DÉPÔT est enregistré par CommandeRepository.Creer, et
//  celui au RETRAIT par CommandeRepository.Retirer (dans la même
//  transaction que la commande). Ce fichier sert pour le reste :
//  historique d'une commande, avance supplémentaire, tableau de bord.
// =====================================================================

using System.Data;
using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table paiement et à la vue vue_encaissements.</summary>
public static class PaiementRepository
{
    private const string Colonnes =
        "id_paiement, id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement";

    private const string SqlInsertion =
        "INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment) " +
        "VALUES (@cmd, @user, @montant, @mode, @moment) RETURNING id_paiement";


    // ------------------------- Lecture -------------------------

    /// <summary>Les paiements d'une commande, du plus ancien au plus récent.</summary>
    public static List<Paiement> ListerParCommande(int idCommande) =>
        Database.LireListe(
            $"SELECT {Colonnes} FROM paiement WHERE id_commande = @id ORDER BY date_paiement, id_paiement",
            VersPaiement, Database.P("@id", idCommande));

    /// <summary>Total encaissé un jour donné (commandes annulées exclues). Tableau de bord.</summary>
    public static int TotalEncaisse(DateTime jour) =>
        Convert.ToInt32(Database.Scalaire(
            "SELECT COALESCE(SUM(montant), 0) FROM vue_encaissements WHERE jour = @jour",
            Database.P("@jour", jour.Date)));

    /// <summary>
    /// Encaissements par jour et par mode entre deux dates (incluses).
    /// Renvoie un DataTable : colonnes jour, mode_paiement, montant, nb_paiements.
    /// Se branche directement sur un DataGridView.
    /// </summary>
    public static DataTable Encaissements(DateTime du, DateTime au) =>
        Database.LireTable(
            "SELECT jour, mode_paiement, montant, nb_paiements FROM vue_encaissements " +
            "WHERE jour BETWEEN @du AND @au ORDER BY jour, mode_paiement",
            Database.P("@du", du.Date),
            Database.P("@au", au.Date));


    // ------------------------- Écriture -------------------------

    /// <summary>Ajoute un paiement seul (ex. une avance en plus). Renvoie son id.</summary>
    public static int Ajouter(Paiement p) =>
        Database.InsererEtRenvoyerId(SqlInsertion, Parametres(p));

    /// <summary>
    /// Ajoute un paiement DANS une transaction déjà ouverte
    /// (utilisé par CommandeRepository pour le dépôt et le retrait).
    /// « internal » = visible seulement à l'intérieur de Pressing.DAL.
    /// </summary>
    internal static int Ajouter(NpgsqlConnection connexion, NpgsqlTransaction transaction, Paiement p) =>
        Convert.ToInt32(Database.Scalaire(connexion, transaction, SqlInsertion, Parametres(p)));


    // ------------------------- Outils -------------------------

    /// <summary>Les paramètres SQL d'un paiement (enums envoyés en texte).</summary>
    private static NpgsqlParameter[] Parametres(Paiement p) =>
    [
        Database.P("@cmd", p.IdCommande),
        Database.P("@user", p.IdUtilisateur),
        Database.P("@montant", p.Montant),
        Database.P("@mode", p.ModePaiement.ToString()),
        Database.P("@moment", p.Moment.ToString())
    ];

    /// <summary>Transforme une ligne de la table en objet Paiement.</summary>
    private static Paiement VersPaiement(NpgsqlDataReader r) => new Paiement
    {
        IdPaiement    = r.Int("id_paiement"),
        IdCommande    = r.Int("id_commande"),
        IdUtilisateur = r.Int("id_utilisateur"),
        Montant       = r.Int("montant"),
        ModePaiement  = r.EnumDe<ModePaiement>("mode_paiement"),
        Moment        = r.EnumDe<MomentPaiement>("moment"),
        DatePaiement  = r.Date("date_paiement")
    };
}
