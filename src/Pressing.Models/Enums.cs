// =====================================================================
//  Enums.cs  —  Les listes de valeurs fixes de PressiGest
// ---------------------------------------------------------------------
//  Une énumération (enum) = une liste FERMÉE de valeurs possibles.
//  Chaque enum ici correspond à une colonne de la base PostgreSQL qui
//  n'accepte que certaines valeurs (contrainte CHECK).
//
//  IMPORTANT : le nom de chaque valeur est EXACTEMENT le texte stocké
//  en base (sans accents, sans espaces). Exemple :
//      en base : 'EnLavage'   <=>   en C# : StatutCommande.EnLavage
//
//  Passer de l'un à l'autre :
//      C# -> base : StatutCommande.EnLavage.ToString()     donne "EnLavage"
//      base -> C# : Enum.Parse<StatutCommande>("EnLavage") donne StatutCommande.EnLavage
//
//  Pour AFFICHER une valeur en français à l'utilisateur, on utilise la
//  classe Libelles en bas de ce fichier : Libelles.De(StatutCommande.EnLavage)
//  donne "En lavage".
// =====================================================================

using System.Globalization;   // nécessaire pour CultureInfo (format des montants)

namespace Pressing.Models;     // toutes les classes de ce projet sont dans « Pressing.Models »


// ---------------------------------------------------------------------
//  Rôle d'un utilisateur (table utilisateur, colonne role)
//  Décide quels écrans l'employé peut ouvrir.
// ---------------------------------------------------------------------
public enum RoleUtilisateur
{
    Administrateur,   // le gérant : accès à tout
    Caissier,         // comptoir : clients, dépôts, paiements, retraits
    Laveur,           // atelier : commence et termine le lavage
    Repasseur         // atelier : termine le repassage (commande « Prête »)
}


// ---------------------------------------------------------------------
//  Statut d'une commande (table commande, colonne statut)
//  Ordre normal : Deposee -> EnLavage -> EnRepassage -> Prete -> Retiree
//  « Repassage seul » commence directement à EnRepassage.
// ---------------------------------------------------------------------
public enum StatutCommande
{
    Deposee,       // enregistrée au comptoir, attend le laveur
    EnLavage,      // le laveur a commencé
    EnRepassage,   // lavée (ou repassage seul), attend le repasseur
    Prete,         // repassée : le client peut venir (email envoyé)
    Avertie,       // prête depuis 30 jours sans retrait : email d'avertissement envoyé
    Retiree,       // le client a récupéré ses vêtements (tout est payé)
    Vendue,        // non retirée après le délai de grâce : vendue par l'administrateur
    Annulee        // annulée par l'administrateur
}


// ---------------------------------------------------------------------
//  Statut de paiement d'une commande (table commande, colonne statut_paiement)
//  Calculé AUTOMATIQUEMENT par la base après chaque paiement : on ne le
//  modifie jamais nous-mêmes en C#.
// ---------------------------------------------------------------------
public enum StatutPaiement
{
    NonPaye,             // rien n'a été versé
    PartiellementPaye,   // une avance a été versée, il reste à payer
    Paye                 // tout est payé
}


// ---------------------------------------------------------------------
//  Mode de paiement (table paiement, colonne mode_paiement)
//  Les paiements sont seulement NOTÉS : aucun paiement en ligne.
// ---------------------------------------------------------------------
public enum ModePaiement
{
    Especes,       // billets / pièces
    OrangeMoney,   // Orange Money
    MobileMoney    // MTN Mobile Money
}


// ---------------------------------------------------------------------
//  Moment du paiement (table paiement, colonne moment)
//  Un client peut payer en une fois ou en deux fois.
// ---------------------------------------------------------------------
public enum MomentPaiement
{
    Depot,     // payé en déposant les vêtements
    Retrait    // payé en venant les récupérer
}


// ---------------------------------------------------------------------
//  Circuit d'un service dans l'atelier (table service, colonne circuit)
//  Décide où part la commande quand on l'enregistre.
// ---------------------------------------------------------------------
public enum Circuit
{
    LavagePuisRepassage,   // laveur puis repasseur (standard, express, nettoyage à sec)
    RepassageSeul          // directement chez le repasseur
}


// ---------------------------------------------------------------------
//  Type d'email envoyé au client (table email_envoye, colonne type)
// ---------------------------------------------------------------------
public enum TypeEmail
{
    Pret,            // « Votre commande est prête »
    Avertissement    // « Vos vêtements seront mis en vente le … »
}


// ---------------------------------------------------------------------
//  Résultat d'un envoi d'email (table email_envoye, colonne resultat)
// ---------------------------------------------------------------------
public enum ResultatEmail
{
    Envoye,   // l'email est bien parti
    Echec     // l'envoi a échoué (pas d'Internet, adresse invalide…)
}


// ---------------------------------------------------------------------
//  Type de notification dans l'application (table notification, colonne type)
//  Les notifications sont créées automatiquement par la base.
// ---------------------------------------------------------------------
public enum TypeNotification
{
    NouvelleCommande,   // pour le laveur ou le repasseur : une commande arrive dans sa file
    EmailEchec,         // pour l'administrateur : un email n'est pas parti, à renvoyer
    AVendre             // pour l'administrateur : le délai de grâce est dépassé
}


// =====================================================================
//  Libelles : traduit les valeurs ci-dessus en texte français lisible.
// ---------------------------------------------------------------------
//  « static » = on n'a pas besoin de créer d'objet, on appelle directement :
//      Libelles.De(StatutCommande.Prete)   donne "Prête"
//      Libelles.Fcfa(3500)                 donne "3 500 FCFA"
//
//  Toutes les méthodes s'appellent « De » : C# choisit la bonne selon le
//  type de la valeur donnée (c'est ce qu'on appelle la surcharge).
//
//  La syntaxe  valeur switch { A => "...", B => "...", _ => ... }  est un
//  « switch expression » : il renvoie le texte qui correspond à la valeur.
//  Le « _ » veut dire « tous les autres cas » (sécurité si on ajoute une
//  valeur un jour sans mettre à jour cette liste).
// =====================================================================
public static class Libelles
{
    /// <summary>Nom du rôle en français.</summary>
    public static string De(RoleUtilisateur r) => r switch
    {
        RoleUtilisateur.Administrateur => "Administrateur",
        RoleUtilisateur.Caissier => "Caissier",
        RoleUtilisateur.Laveur => "Laveur",
        RoleUtilisateur.Repasseur => "Repasseur",
        _ => r.ToString()
    };

    /// <summary>Statut de commande avec accents et espaces (pastilles, tableaux, ticket).</summary>
    public static string De(StatutCommande s) => s switch
    {
        StatutCommande.Deposee => "Déposée",
        StatutCommande.EnLavage => "En lavage",
        StatutCommande.EnRepassage => "En repassage",
        StatutCommande.Prete => "Prête",
        StatutCommande.Avertie => "Avertie",
        StatutCommande.Retiree => "Retirée",
        StatutCommande.Vendue => "Vendue",
        StatutCommande.Annulee => "Annulée",
        _ => s.ToString()
    };

    /// <summary>Statut de paiement en français.</summary>
    public static string De(StatutPaiement s) => s switch
    {
        StatutPaiement.NonPaye => "Non payé",
        StatutPaiement.PartiellementPaye => "Partiellement payé",
        StatutPaiement.Paye => "Payé",
        _ => s.ToString()
    };

    /// <summary>Mode de paiement en français.</summary>
    public static string De(ModePaiement m) => m switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.OrangeMoney => "Orange Money",
        ModePaiement.MobileMoney => "MTN Mobile Money",
        _ => m.ToString()
    };

    /// <summary>Moment du paiement en français.</summary>
    public static string De(MomentPaiement m) => m switch
    {
        MomentPaiement.Depot => "Au dépôt",
        MomentPaiement.Retrait => "Au retrait",
        _ => m.ToString()
    };

    /// <summary>Circuit d'un service en français.</summary>
    public static string De(Circuit c) => c switch
    {
        Circuit.LavagePuisRepassage => "Lavage puis repassage",
        Circuit.RepassageSeul => "Repassage seul",
        _ => c.ToString()
    };

    /// <summary>Type d'email en français (journal des emails).</summary>
    public static string De(TypeEmail t) => t switch
    {
        TypeEmail.Pret => "Commande prête",
        TypeEmail.Avertissement => "Avertissement de mise en vente",
        _ => t.ToString()
    };

    /// <summary>Résultat d'un envoi d'email en français.</summary>
    public static string De(ResultatEmail r) => r switch
    {
        ResultatEmail.Envoye => "Envoyé",
        ResultatEmail.Echec => "Échec",
        _ => r.ToString()
    };

    /// <summary>Type de notification en français (écran Notifications).</summary>
    public static string De(TypeNotification t) => t switch
    {
        TypeNotification.NouvelleCommande => "Nouvelle commande",
        TypeNotification.EmailEchec => "Email non envoyé",
        TypeNotification.AVendre => "Commande à vendre",
        _ => t.ToString()
    };

    /// <summary>
    /// Écrit un montant en FCFA avec un espace entre les milliers.
    /// Exemple : Fcfa(3500) donne "3 500 FCFA".
    /// </summary>
    public static string Fcfa(int montant) =>
        // "N0" = nombre sans décimales avec séparateur de milliers.
        // InvariantCulture donne "3,500" ; on remplace la virgule par un espace.
        montant.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ') + " FCFA";
}
