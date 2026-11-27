// =====================================================================
//  Notification.cs  —  Une alerte affichée dans l'application
// ---------------------------------------------------------------------
//  Correspond à la table « notification » de PostgreSQL.
//  Une notification n'est PAS un email : c'est un message qui apparaît
//  dans l'application (cloche, liste « À faire ») pour un RÔLE précis.
//  Tous les employés qui ont ce rôle la voient.
//
//  Elles sont créées AUTOMATIQUEMENT par la base (triggers) :
//    NouvelleCommande -> pour le Laveur (lavage, express, sec...)
//                        ou pour le Repasseur (service « repassage seul »)
//                        dès qu'une commande arrive dans sa file ;
//    EmailEchec       -> pour l'Administrateur : un email n'est pas parti ;
//    AVendre          -> pour l'Administrateur : le délai de grâce après
//                        l'avertissement est dépassé, la commande peut
//                        être vendue (créée par fn_generer_notifications_a_vendre).
//    ALivrer          -> pour le Livreur, DÈS LE DÉPÔT quand le client a
//                        choisi la livraison : une livraison est à prévoir ;
//    PreteALivrer     -> pour le Livreur : la commande est prête, il peut
//                        partir (aussi après un échec de livraison).
//
//  Le C# fait seulement deux choses :
//    1. LIRE les notifications non traitées du rôle de l'utilisateur connecté ;
//    2. les marquer comme TRAITÉES (Traitee = true + date) quand c'est fait.
//
//  Une même commande ne reçoit jamais deux fois la même notification
//  pour le même rôle (contrainte UNIQUE id_commande + type + pour_role).
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_notification             ->   IdNotification
//      id_commande                 ->   IdCommande
//      pour_role                   ->   PourRole
//      type                        ->   Type
//      date_creation               ->   DateCreation
//      traitee                     ->   Traitee
//      date_traitement             ->   DateTraitement   (vide si non traitée)
//
//  Utilisé par : la zone de notifications de la fenêtre principale
//  (laveur, repasseur, livreur, administrateur).
// =====================================================================

namespace Pressing.Models;

/// <summary>Une alerte dans l'application destinée à un rôle (table notification, créée par la base).</summary>
public class Notification
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de la notification.</summary>
    public int IdNotification { get; set; }

    /// <summary>La commande concernée.</summary>
    public int IdCommande { get; set; }

    /// <summary>Le rôle qui doit voir la notification (Laveur, Repasseur, Livreur, Administrateur...).</summary>
    public RoleUtilisateur PourRole { get; set; }

    /// <summary>Nouvelle commande, email non envoyé, ou commande à vendre.</summary>
    public TypeNotification Type { get; set; }

    /// <summary>Date et heure de création (remplie par la base).</summary>
    public DateTime DateCreation { get; set; }

    /// <summary>Vrai quand un employé a marqué la notification comme faite.</summary>
    public bool Traitee { get; set; }

    /// <summary>Quand elle a été traitée (vide tant qu'elle ne l'est pas).</summary>
    public DateTime? DateTraitement { get; set; }


    // ---------- Affichage (jointure avec commande, client et service) ----------

    /// <summary>Numéro du ticket de la commande.</summary>
    public string NumeroTicket { get; set; } = string.Empty;

    /// <summary>Nom complet du client.</summary>
    public string NomClient { get; set; } = string.Empty;

    /// <summary>Téléphone du client (utile pour l'appeler si l'email a échoué).</summary>
    public string TelephoneClient { get; set; } = string.Empty;

    /// <summary>Nom du service : le laveur voit tout de suite s'il s'agit d'un express, d'un sec...</summary>
    public string NomService { get; set; } = string.Empty;


    // ------------------------- Affichage -------------------------

    /// <summary>Exemple : « Nouvelle commande : PR-2026-000002 - Lavage express (Ngo Marie) ».</summary>
    public override string ToString() =>
        $"{Libelles.De(Type)} : {NumeroTicket} - {NomService} ({NomClient})";
}
