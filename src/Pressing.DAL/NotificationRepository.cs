// =====================================================================
//  NotificationRepository.cs  —  Alertes dans l'application
// ---------------------------------------------------------------------
//  Les notifications sont CRÉÉES par la base :
//    - NouvelleCommande -> Laveur ou Repasseur (trigger trg_commande_apres) ;
//    - EmailEchec       -> Administrateur      (trigger trg_email_echec) ;
//    - AVendre          -> Administrateur      (fonction fn_generer_notifications_a_vendre).
//  Elles sont aussi marquées « traitées » automatiquement quand la
//  commande quitte la file ou se termine.
//
//  Le C# :
//    - LIT les notifications non traitées du rôle connecté ;
//    - les COMPTE (pastille sur la cloche) ;
//    - les MARQUE comme traitées à la main (bouton « OK ») ;
//    - lance la génération des « À vendre » au démarrage.
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table notification.</summary>
public static class NotificationRepository
{
    /// <summary>Notifications non traitées d'un rôle, les plus récentes d'abord.</summary>
    public static List<Notification> ListerNonTraitees(RoleUtilisateur role) =>
        Database.LireListe(
            "SELECT n.id_notification, n.id_commande, n.pour_role, n.type, n.date_creation, " +
            "n.traitee, n.date_traitement, v.numero_ticket, v.nom_client, v.telephone, v.service " +
            "FROM notification n " +
            "JOIN vue_commande_solde v ON v.id_commande = n.id_commande " +
            "WHERE n.pour_role = @role AND NOT n.traitee " +
            "ORDER BY n.date_creation DESC, n.id_notification DESC",
            VersNotification, Database.P("@role", role.ToString()));

    /// <summary>Nombre de notifications non traitées d'un rôle (pastille de la cloche).</summary>
    public static int CompterNonTraitees(RoleUtilisateur role) =>
        Convert.ToInt32(Database.Scalaire(
            "SELECT COUNT(*) FROM notification WHERE pour_role = @role AND NOT traitee",
            Database.P("@role", role.ToString())));

    /// <summary>Marque une notification comme traitée. Renvoie false si elle l'était déjà.</summary>
    public static bool MarquerTraitee(int idNotification) =>
        Database.Executer(
            "UPDATE notification SET traitee = TRUE, date_traitement = LOCALTIMESTAMP(0) " +
            "WHERE id_notification = @id AND NOT traitee",
            Database.P("@id", idNotification)) > 0;

    /// <summary>
    /// Crée les notifications « À vendre » pour l'administrateur (commandes
    /// averties depuis plus de DelaiGraceJours). À appeler au démarrage.
    /// Renvoie le nombre de NOUVELLES notifications créées.
    /// </summary>
    public static int GenererAVendre() =>
        Database.EnTransaction((connexion, transaction) =>
            Convert.ToInt32(Database.Scalaire(connexion, transaction,
                "SELECT fn_generer_notifications_a_vendre()")));

    /// <summary>Transforme une ligne en objet Notification.</summary>
    private static Notification VersNotification(NpgsqlDataReader r) => new Notification
    {
        IdNotification  = r.Int("id_notification"),
        IdCommande      = r.Int("id_commande"),
        PourRole        = r.EnumDe<RoleUtilisateur>("pour_role"),
        Type            = r.EnumDe<TypeNotification>("type"),
        DateCreation    = r.Date("date_creation"),
        Traitee         = r.Bool("traitee"),
        DateTraitement  = r.DateOuNull("date_traitement"),
        NumeroTicket    = r.Texte("numero_ticket"),
        NomClient       = r.Texte("nom_client"),
        TelephoneClient = r.Texte("telephone"),
        NomService      = r.Texte("service")
    };
}
