// =====================================================================
//  ErreurBase.cs  —  Traduire les erreurs de PostgreSQL en français
// ---------------------------------------------------------------------
//  Quand la base refuse une opération, elle renvoie un CODE d'erreur
//  (SqlState). Cette classe transforme ce code en un message clair à
//  afficher à l'employé.
//
//  1) Nos erreurs MÉTIER (levées par nos triggers, message déjà en français) :
//       PR001  paiement supérieur au total de la commande
//       PR003  retrait impossible : il reste à payer
//       PR005  changement de statut interdit
//       PR006  service non modifiable après le début du traitement
//
//  2) Les erreurs STANDARD de PostgreSQL :
//       23505  doublon (téléphone, email, login déjà utilisé...)
//       23503  lien avec d'autres données (suppression impossible)
//       23514  valeur refusée par une règle CHECK (email mal écrit...)
//       28P01  mauvais mot de passe PostgreSQL
//       3D000  la base pressing_db n'existe pas
//
//  Utilisation dans l'UI :
//      try { ... }
//      catch (Exception ex)
//      {
//          MessageBox.Show(ErreurBase.Message(ex), "Opération impossible");
//      }
// =====================================================================

using Npgsql;

namespace Pressing.DAL;

/// <summary>Transforme une erreur technique en message compréhensible.</summary>
public static class ErreurBase
{
    /// <summary>Renvoie toujours un message en français à afficher à l'utilisateur.</summary>
    public static string Message(Exception ex)
    {
        // PostgresException = la base a répondu, mais a refusé l'opération.
        // (À tester AVANT NpgsqlException, car c'est un cas particulier de celle-ci.)
        if (ex is PostgresException pg)
            return MessagePostgres(pg);

        // NpgsqlException = la base n'a pas pu être jointe (arrêtée, mauvais port...)
        if (ex is NpgsqlException)
            return "Impossible de joindre la base de données. Vérifiez que PostgreSQL est démarré " +
                   "et que les informations de appsettings.json sont correctes.";

        // Autres erreurs (fichier appsettings.json manquant, etc.) : leur message est déjà clair
        return ex.Message;
    }

    /// <summary>Vrai si c'est une de NOS règles de gestion (codes PR...).</summary>
    public static bool EstErreurMetier(Exception ex) =>
        ex is PostgresException pg && pg.SqlState.StartsWith("PR", StringComparison.Ordinal);


    // ------------------------- Détail des codes -------------------------

    private static string MessagePostgres(PostgresException pg)
    {
        // Nos triggers : le message est déjà écrit en français dans le SQL
        if (pg.SqlState.StartsWith("PR", StringComparison.Ordinal))
            return pg.MessageText;

        switch (pg.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:        // 23505
                return MessageDoublon(pg.ConstraintName);

            case PostgresErrorCodes.ForeignKeyViolation:    // 23503
                return "Opération impossible : cet élément est utilisé ailleurs (commandes, tarifs...). " +
                       "Désactivez-le plutôt que de le supprimer.";

            case PostgresErrorCodes.CheckViolation:         // 23514
                return pg.ConstraintName == "ck_client_email"
                    ? "L'adresse email n'est pas valide (exemple attendu : nom@domaine.com)."
                    : "Valeur refusée : un montant, une quantité ou un choix n'est pas autorisé.";

            case PostgresErrorCodes.NotNullViolation:       // 23502
                return "Un champ obligatoire n'a pas été rempli.";

            case PostgresErrorCodes.InvalidPassword:        // 28P01
                return "Mot de passe PostgreSQL incorrect : corrigez-le dans appsettings.json.";

            case PostgresErrorCodes.InvalidCatalogName:     // 3D000
                return "La base pressing_db n'existe pas : créez-la dans pgAdmin puis lancez les scripts du dossier database.";

            default:
                return "Erreur de la base de données : " + pg.MessageText;
        }
    }

    /// <summary>Message précis selon la contrainte UNIQUE qui a bloqué.</summary>
    private static string MessageDoublon(string? contrainte) => contrainte switch
    {
        "client_telephone_key"      => "Ce numéro de téléphone est déjà utilisé par un autre client.",
        "client_email_key"          => "Cette adresse email est déjà utilisée par un autre client.",
        "utilisateur_login_key"     => "Cet identifiant de connexion est déjà pris.",
        "service_nom_key"           => "Un service porte déjà ce nom.",
        "type_vetement_libelle_key" => "Ce type de vêtement existe déjà.",
        "uq_email_automatique"      => "Cet email a déjà été envoyé avec succès pour cette commande.",
        _                           => "Cette valeur existe déjà (doublon)."
    };
}
