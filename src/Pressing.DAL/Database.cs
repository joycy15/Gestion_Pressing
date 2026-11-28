// =====================================================================
//  Database.cs  —  La porte d'entrée vers PostgreSQL
// ---------------------------------------------------------------------
//  TOUTES les requêtes de l'application passent par cette classe.
//  Les Repository (ClientRepository, CommandeRepository...) l'utilisent ;
//  la BLL et l'UI ne l'appellent jamais directement pour faire du SQL.
//
//  Ce qu'on y trouve :
//    1. Ouvrir une connexion / tester que la base répond.
//    2. P(...)            : créer un paramètre SQL (@nom).
//    3. Lectures          : LireListe, LireUn, Scalaire, LireTable.
//    4. Écritures         : Executer, InsererEtRenvoyerId.
//    5. Transactions      : EnTransaction (tout est enregistré, ou rien).
//
//  SÉCURITÉ : on utilise TOUJOURS des paramètres (@nom), jamais de texte
//  saisi par l'utilisateur collé dans la requête -> pas d'injection SQL.
//
//  QUI A FAIT LE CHANGEMENT ?
//    Après la connexion, la BLL remplit Database.IdUtilisateurConnecte.
//    Chaque écriture se fait dans une transaction qui commence par :
//        SELECT set_config('app.id_utilisateur', '<id>', true);
//    Les triggers lisent cette valeur pour remplir historique_statut.
//    (« true » = la valeur ne dure que le temps de la transaction.)
// =====================================================================

using System.Data;   // DataTable
using Npgsql;        // pilote PostgreSQL pour .NET (paquet NuGet Npgsql)

namespace Pressing.DAL;

/// <summary>Accès commun à la base PostgreSQL pressing_db.</summary>
public static class Database
{
    // =====================================================================
    //  Utilisateur connecté
    // =====================================================================

    /// <summary>
    /// Id de l'employé connecté. Rempli par la BLL après la connexion,
    /// remis à null à la déconnexion. Envoyé à la base dans chaque transaction.
    /// </summary>
    public static int? IdUtilisateurConnecte { get; set; }


    // =====================================================================
    //  1. Connexion
    // =====================================================================

    /// <summary>Ouvre et renvoie une connexion. Toujours l'utiliser avec « using ».</summary>
    public static NpgsqlConnection OuvrirConnexion()
    {
        var connexion = new NpgsqlConnection(Configuration.ChaineConnexion);
        connexion.Open();
        return connexion;
    }

    /// <summary>
    /// Vérifie que la base répond (à appeler au démarrage de l'application).
    /// Renvoie true si tout va bien ; sinon false + un message en français.
    /// </summary>
    public static bool TesterConnexion(out string erreur)
    {
        try
        {
            using NpgsqlConnection connexion = OuvrirConnexion();
            erreur = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            erreur = ErreurBase.Message(ex);
            return false;
        }
    }


    // =====================================================================
    //  2. Paramètres
    // =====================================================================

    /// <summary>
    /// Crée un paramètre SQL. Une valeur null devient NULL en base (DBNull.Value).
    /// Exemple : Database.P("@id", 5)
    /// </summary>
    public static NpgsqlParameter P(string nom, object? valeur) =>
        new NpgsqlParameter(nom, valeur ?? DBNull.Value);


    // =====================================================================
    //  3. Lectures (une connexion par appel)
    // =====================================================================

    /// <summary>
    /// SELECT -> liste d'objets. « lire » transforme UNE ligne en UN objet.
    /// Exemple :
    ///   Database.LireListe("SELECT ... FROM client", VersClient);
    /// </summary>
    public static List<T> LireListe<T>(string sql, Func<NpgsqlDataReader, T> lire, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection connexion = OuvrirConnexion();
        return LireListe(connexion, null, sql, lire, parametres);
    }

    /// <summary>SELECT -> un seul objet, ou null si aucune ligne trouvée.</summary>
    public static T? LireUn<T>(string sql, Func<NpgsqlDataReader, T> lire, params NpgsqlParameter[] parametres)
        where T : class
    {
        List<T> liste = LireListe(sql, lire, parametres);
        return liste.Count > 0 ? liste[0] : null;
    }

    /// <summary>
    /// Renvoie la première colonne de la première ligne (COUNT, SUM, EXISTS...).
    /// Renvoie null si le résultat est NULL ou s'il n'y a aucune ligne.
    /// </summary>
    public static object? Scalaire(string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection connexion = OuvrirConnexion();
        return Scalaire(connexion, null, sql, parametres);
    }

    /// <summary>SELECT -> DataTable (pratique pour remplir directement un DataGridView).</summary>
    public static DataTable LireTable(string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection connexion = OuvrirConnexion();
        using NpgsqlCommand commande = CreerCommande(connexion, null, sql, parametres);
        using NpgsqlDataReader lecteur = commande.ExecuteReader();

        var table = new DataTable();
        table.Load(lecteur);
        return table;
    }


    // =====================================================================
    //  4. Écritures simples (chacune dans sa propre petite transaction)
    // =====================================================================

    /// <summary>INSERT / UPDATE / DELETE. Renvoie le nombre de lignes touchées.</summary>
    public static int Executer(string sql, params NpgsqlParameter[] parametres) =>
        EnTransaction((connexion, transaction) => Executer(connexion, transaction, sql, parametres));

    /// <summary>
    /// INSERT ... RETURNING id -> renvoie l'id créé par la base.
    /// Exemple : int id = Database.InsererEtRenvoyerId("INSERT ... RETURNING id_client", ...);
    /// </summary>
    public static int InsererEtRenvoyerId(string sql, params NpgsqlParameter[] parametres) =>
        EnTransaction((connexion, transaction) =>
            Convert.ToInt32(Scalaire(connexion, transaction, sql, parametres)));


    // =====================================================================
    //  5. Transactions : plusieurs requêtes, tout ou rien
    // =====================================================================

    /// <summary>
    /// Exécute « travail » dans une transaction.
    ///   - si tout se passe bien : Commit (tout est enregistré) ;
    ///   - si une erreur arrive  : rien n'est enregistré (Rollback automatique
    ///     quand la transaction est fermée par le « using » sans Commit).
    /// L'id de l'utilisateur connecté est envoyé à la base au début.
    /// </summary>
    public static T EnTransaction<T>(Func<NpgsqlConnection, NpgsqlTransaction, T> travail)
    {
        using NpgsqlConnection connexion = OuvrirConnexion();
        using NpgsqlTransaction transaction = connexion.BeginTransaction();

        // Dire à la base QUI fait les modifications (pour historique_statut)
        if (IdUtilisateurConnecte is int id)
        {
            Executer(connexion, transaction,
                "SELECT set_config('app.id_utilisateur', @id, true)",
                P("@id", id.ToString()));
        }

        T resultat = travail(connexion, transaction);
        transaction.Commit();   // si on n'arrive pas ici, rien n'est enregistré
        return resultat;
    }

    /// <summary>Même chose, pour un travail qui ne renvoie rien.</summary>
    public static void EnTransaction(Action<NpgsqlConnection, NpgsqlTransaction> travail) =>
        EnTransaction<bool>((connexion, transaction) =>
        {
            travail(connexion, transaction);
            return true;
        });


    // ---------- Versions à utiliser À L'INTÉRIEUR d'une transaction ----------

    /// <summary>INSERT / UPDATE / DELETE dans une transaction déjà ouverte.</summary>
    public static int Executer(NpgsqlConnection connexion, NpgsqlTransaction? transaction,
                               string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlCommand commande = CreerCommande(connexion, transaction, sql, parametres);
        return commande.ExecuteNonQuery();
    }

    /// <summary>Première colonne de la première ligne, dans une transaction déjà ouverte.</summary>
    public static object? Scalaire(NpgsqlConnection connexion, NpgsqlTransaction? transaction,
                                   string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlCommand commande = CreerCommande(connexion, transaction, sql, parametres);
        object? resultat = commande.ExecuteScalar();
        return resultat == DBNull.Value ? null : resultat;
    }

    /// <summary>SELECT -> liste d'objets, dans une transaction déjà ouverte.</summary>
    public static List<T> LireListe<T>(NpgsqlConnection connexion, NpgsqlTransaction? transaction,
                                       string sql, Func<NpgsqlDataReader, T> lire,
                                       params NpgsqlParameter[] parametres)
    {
        var liste = new List<T>();
        using NpgsqlCommand commande = CreerCommande(connexion, transaction, sql, parametres);
        using NpgsqlDataReader lecteur = commande.ExecuteReader();

        while (lecteur.Read())          // une ligne à la fois
            liste.Add(lire(lecteur));   // on la transforme en objet

        return liste;
    }


    // =====================================================================
    //  Outil interne
    // =====================================================================

    /// <summary>Prépare une commande SQL avec ses paramètres.</summary>
    private static NpgsqlCommand CreerCommande(NpgsqlConnection connexion, NpgsqlTransaction? transaction,
                                               string sql, NpgsqlParameter[] parametres)
    {
        var commande = new NpgsqlCommand(sql, connexion, transaction);
        if (parametres.Length > 0)
            commande.Parameters.AddRange(parametres);
        return commande;
    }
}
