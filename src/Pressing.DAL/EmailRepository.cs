// =====================================================================
//  EmailRepository.cs  —  Journal des emails (table email_envoye)
// ---------------------------------------------------------------------
//  La DAL n'ENVOIE pas d'email : c'est la BLL (MailKit / SMTP) qui le
//  fait. Après CHAQUE tentative, la BLL appelle Enregistrer(...) pour
//  noter le résultat (RG16).
//
//  Ce que fait la base toute seule :
//    - si le résultat est « Echec », elle crée une notification
//      « EmailEchec » pour l'administrateur ;
//    - elle refuse un 2e email AUTOMATIQUE réussi du même type pour la
//      même commande (index uq_email_automatique). Un renvoi MANUEL
//      (Manuel = true) est toujours accepté.
//
//  Utilisé par : le service d'emails (BLL) et l'écran « Journal des
//  emails » de l'administrateur.
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table email_envoye.</summary>
public static class EmailRepository
{
    // Colonnes de l'email + numéro de ticket de la commande
    private const string SelectEmail =
        "SELECT e.id_email, e.id_commande, e.type, e.destinataire, e.date_envoi, e.resultat, " +
        "e.erreur, e.manuel, c.numero_ticket " +
        "FROM email_envoye e JOIN commande c ON c.id_commande = e.id_commande ";

    // La colonne erreur fait 500 caractères maximum
    private const int LongueurMaxErreur = 500;


    // ------------------------- Écriture -------------------------

    /// <summary>Note une tentative d'envoi (réussie ou non). Renvoie l'id de la ligne.</summary>
    public static int Enregistrer(EmailEnvoye email)
    {
        // Message d'erreur technique coupé à 500 caractères (sinon la base refuse)
        string? erreur = email.Erreur;
        if (erreur is not null && erreur.Length > LongueurMaxErreur)
            erreur = erreur.Substring(0, LongueurMaxErreur);

        return Database.InsererEtRenvoyerId(
            "INSERT INTO email_envoye (id_commande, type, destinataire, resultat, erreur, manuel) " +
            "VALUES (@cmd, @type, @dest, @resultat, @erreur, @manuel) RETURNING id_email",
            Database.P("@cmd", email.IdCommande),
            Database.P("@type", email.Type.ToString()),
            Database.P("@dest", email.Destinataire),
            Database.P("@resultat", email.Resultat.ToString()),
            Database.P("@erreur", erreur),
            Database.P("@manuel", email.Manuel));
    }


    // ------------------------- Lecture -------------------------

    /// <summary>Les derniers emails (les plus récents d'abord). Écran Journal des emails.</summary>
    public static List<EmailEnvoye> ListerRecents(int nombre = 200) =>
        Database.LireListe(SelectEmail + "ORDER BY e.date_envoi DESC, e.id_email DESC LIMIT @n",
            VersEmail, Database.P("@n", nombre));

    /// <summary>Les emails d'une commande.</summary>
    public static List<EmailEnvoye> ListerParCommande(int idCommande) =>
        Database.LireListe(SelectEmail + "WHERE e.id_commande = @id ORDER BY e.date_envoi, e.id_email",
            VersEmail, Database.P("@id", idCommande));

    /// <summary>Vrai si cet email a déjà été envoyé AVEC SUCCÈS pour cette commande.</summary>
    public static bool DejaEnvoye(int idCommande, TypeEmail type) =>
        (bool)Database.Scalaire(
            "SELECT EXISTS (SELECT 1 FROM email_envoye " +
            "WHERE id_commande = @id AND type = @type AND resultat = 'Envoye')",
            Database.P("@id", idCommande), Database.P("@type", type.ToString()))!;


    // ------------------------- Ligne -> objet -------------------------

    private static EmailEnvoye VersEmail(NpgsqlDataReader r) => new EmailEnvoye
    {
        IdEmail      = r.Int("id_email"),
        IdCommande   = r.Int("id_commande"),
        Type         = r.EnumDe<TypeEmail>("type"),
        Destinataire = r.Texte("destinataire"),
        DateEnvoi    = r.Date("date_envoi"),
        Resultat     = r.EnumDe<ResultatEmail>("resultat"),
        Erreur       = r.TexteOuNull("erreur"),
        Manuel       = r.Bool("manuel"),
        NumeroTicket = r.Texte("numero_ticket")
    };
}
