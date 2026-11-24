using System.Data;
using System.Text.Json;
using Npgsql;

namespace Pressing.DAL;

/// <summary>
/// Accès commun à la base PostgreSQL pressing_db.
/// Toutes les requêtes passent par ici, TOUJOURS avec des paramètres (@nom) :
/// jamais de concaténation de texte saisi par l'utilisateur (injection SQL).
///
/// Rappel : les colonnes PostgreSQL sont en snake_case (id_commande, numero_ticket),
/// les propriétés C# en PascalCase (IdCommande, NumeroTicket).
/// </summary>
public static class Database
{
    private static string? _chaineConnexion;

    /// <summary>Chaîne de connexion lue dans appsettings.json (jamais envoyé sur GitHub).</summary>
    public static string ChaineConnexion => _chaineConnexion ??= ChargerChaineConnexion();

    private static string ChargerChaineConnexion()
    {
        string chemin = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(chemin))
            throw new FileNotFoundException(
                "Fichier appsettings.json introuvable à côté de l'exécutable. " +
                "Copiez appsettings.example.json en appsettings.json dans Pressing.UI, " +
                "mettez votre mot de passe PostgreSQL, et réglez « Copier dans le répertoire de sortie ».",
                chemin);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(chemin));
        string? chaine = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("PressingDB")
            .GetString();

        if (string.IsNullOrWhiteSpace(chaine))
            throw new InvalidOperationException("La chaîne de connexion PressingDB est vide dans appsettings.json.");

        return chaine;
    }

    /// <summary>Ouvre et renvoie une connexion. À utiliser avec « using ».</summary>
    public static NpgsqlConnection OuvrirConnexion()
    {
        var connexion = new NpgsqlConnection(ChaineConnexion);
        connexion.Open();
        return connexion;
    }

    /// <summary>Vérifie que la base est joignable (à appeler au démarrage de l'application).</summary>
    public static bool TesterConnexion(out string erreur)
    {
        try
        {
            using NpgsqlConnection c = OuvrirConnexion();
            erreur = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            erreur = ex.Message;
            return false;
        }
    }

    /// <summary>Crée un paramètre ; null devient DBNull.Value. Exemple : Database.P("@id", 5)</summary>
    public static NpgsqlParameter P(string nom, object? valeur) => new(nom, valeur ?? DBNull.Value);

    // ------------------------------------------------------------------
    //  Requêtes simples (une connexion par appel)
    // ------------------------------------------------------------------

    /// <summary>INSERT / UPDATE / DELETE. Renvoie le nombre de lignes touchées.</summary>
    public static int Executer(string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection c = OuvrirConnexion();
        using NpgsqlCommand cmd = CreerCommande(c, null, sql, parametres);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Renvoie la première colonne de la première ligne (COUNT, SUM, RETURNING id…).
    /// Exemple : int id = Convert.ToInt32(Database.Scalaire("INSERT ... RETURNING id_client", ...));
    /// </summary>
    public static object? Scalaire(string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection c = OuvrirConnexion();
        using NpgsqlCommand cmd = CreerCommande(c, null, sql, parametres);
        object? r = cmd.ExecuteScalar();
        return r == DBNull.Value ? null : r;
    }

    /// <summary>SELECT -> DataTable (pratique pour remplir un DataGridView).</summary>
    public static DataTable Lire(string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlConnection c = OuvrirConnexion();
        using NpgsqlCommand cmd = CreerCommande(c, null, sql, parametres);
        using NpgsqlDataReader reader = cmd.ExecuteReader();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    /// <summary>
    /// SELECT -> liste d'objets. « mapper » transforme une ligne en objet. Exemple :
    /// <code>
    /// var clients = Database.LireListe("SELECT * FROM client ORDER BY nom", r => new Client
    /// {
    ///     IdClient  = r.GetInt32(r.GetOrdinal("id_client")),
    ///     Nom       = r.GetString(r.GetOrdinal("nom")),
    ///     Prenom    = Database.TexteOuNull(r, "prenom"),
    ///     Telephone = r.GetString(r.GetOrdinal("telephone"))
    /// });
    /// </code>
    /// </summary>
    public static List<T> LireListe<T>(string sql, Func<NpgsqlDataReader, T> mapper, params NpgsqlParameter[] parametres)
    {
        var liste = new List<T>();
        using NpgsqlConnection c = OuvrirConnexion();
        using NpgsqlCommand cmd = CreerCommande(c, null, sql, parametres);
        using NpgsqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
            liste.Add(mapper(reader));
        return liste;
    }

    // ------------------------------------------------------------------
    //  Petites aides pour lire les colonnes qui peuvent être NULL
    // ------------------------------------------------------------------

    public static string? TexteOuNull(NpgsqlDataReader r, string colonne)
    {
        int i = r.GetOrdinal(colonne);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static DateTime? DateOuNull(NpgsqlDataReader r, string colonne)
    {
        int i = r.GetOrdinal(colonne);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    // ------------------------------------------------------------------
    //  Transactions (dépôt : commande + lignes + paiement en une seule fois)
    // ------------------------------------------------------------------

    /// <summary>
    /// Exécute plusieurs requêtes dans une transaction : tout est enregistré, ou rien.
    /// Exemple (membre 4, dépôt) :
    /// <code>
    /// int idCommande = Database.EnTransaction((c, t) =>
    /// {
    ///     int id = Convert.ToInt32(Database.Scalaire(c, t,
    ///         "INSERT INTO commande (id_client, id_utilisateur, date_retrait_prevue) " +
    ///         "VALUES (@client, @user, @retrait) RETURNING id_commande",
    ///         Database.P("@client", idClient), Database.P("@user", idUser), Database.P("@retrait", dateRetrait)));
    ///
    ///     foreach (var l in lignes)
    ///         Database.Executer(c, t,
    ///             "INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) " +
    ///             "VALUES (@cmd, @type, @qte, @prix, @obs)",
    ///             Database.P("@cmd", id), Database.P("@type", l.IdTypeVetement), Database.P("@qte", l.Quantite),
    ///             Database.P("@prix", l.PrixUnitaire), Database.P("@obs", l.Observation));
    ///
    ///     // paiement APRÈS les lignes (sinon le total vaut 0 et la base refuse)
    ///     return id;
    /// });
    /// </code>
    /// </summary>
    public static T EnTransaction<T>(Func<NpgsqlConnection, NpgsqlTransaction, T> travail)
    {
        using NpgsqlConnection c = OuvrirConnexion();
        using NpgsqlTransaction t = c.BeginTransaction();
        try
        {
            T resultat = travail(c, t);
            t.Commit();
            return resultat;
        }
        catch
        {
            t.Rollback();
            throw;
        }
    }

    public static void EnTransaction(Action<NpgsqlConnection, NpgsqlTransaction> travail) =>
        EnTransaction<object?>((c, t) => { travail(c, t); return null; });

    /// <summary>Version de Executer à utiliser à l'intérieur de EnTransaction.</summary>
    public static int Executer(NpgsqlConnection c, NpgsqlTransaction t, string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlCommand cmd = CreerCommande(c, t, sql, parametres);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Version de Scalaire à utiliser à l'intérieur de EnTransaction.</summary>
    public static object? Scalaire(NpgsqlConnection c, NpgsqlTransaction t, string sql, params NpgsqlParameter[] parametres)
    {
        using NpgsqlCommand cmd = CreerCommande(c, t, sql, parametres);
        object? r = cmd.ExecuteScalar();
        return r == DBNull.Value ? null : r;
    }

    // ------------------------------------------------------------------
    //  Fonctions de la base
    // ------------------------------------------------------------------

    /// <summary>
    /// RG13 : crée les notifications « Non retirée » (commandes déposées depuis plus de 14 jours).
    /// À appeler au démarrage de l'application. Renvoie le nombre de nouvelles notifications.
    /// </summary>
    public static int GenererNotificationsNonRetirees() =>
        Convert.ToInt32(Scalaire("SELECT fn_generer_notifications_non_retirees()"));

    // ------------------------------------------------------------------
    //  Erreurs métier renvoyées par les déclencheurs PostgreSQL
    // ------------------------------------------------------------------

    /// <summary>
    /// Les déclencheurs lèvent des erreurs PR001 à PR004 avec un message en français
    /// (paiement trop grand, retrait impossible, statut figé…).
    /// Renvoie un message à afficher à l'utilisateur, ou null si c'est une autre erreur.
    /// <code>
    /// catch (Exception ex)
    /// {
    ///     MessageBox.Show(Database.MessageMetier(ex) ?? "Erreur : " + ex.Message);
    /// }
    /// </code>
    /// </summary>
    public static string? MessageMetier(Exception ex)
    {
        if (ex is PostgresException pg)
        {
            if (pg.SqlState.StartsWith("PR")) return pg.MessageText;
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return "Cette valeur existe déjà (doublon : téléphone, login ou libellé déjà utilisé).";
            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                return "Opération impossible : cet élément est lié à d'autres enregistrements.";
            if (pg.SqlState == PostgresErrorCodes.CheckViolation)
                return "Valeur non autorisée (montant, quantité ou statut invalide).";
        }
        return null;
    }

    // ------------------------------------------------------------------

    private static NpgsqlCommand CreerCommande(NpgsqlConnection c, NpgsqlTransaction? t, string sql, NpgsqlParameter[] parametres)
    {
        var cmd = new NpgsqlCommand(sql, c, t);
        if (parametres.Length > 0)
            cmd.Parameters.AddRange(parametres);
        return cmd;
    }
}
