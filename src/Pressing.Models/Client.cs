// =====================================================================
//  Client.cs  —  Un client du pressing
// ---------------------------------------------------------------------
//  Correspond à la table « client » de PostgreSQL.
//  Une ligne de la table  <=>  un objet Client en C#.
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_client                   ->   IdClient
//      nom                         ->   Nom
//      prenom                      ->   Prenom        (facultatif)
//      telephone                   ->   Telephone     (obligatoire, unique)
//      email                       ->   Email         (obligatoire, unique)
//      adresse                     ->   Adresse       (facultatif)
//      date_inscription            ->   DateInscription
//
//  Utilisé par : écran Clients, écran Nouveau dépôt, envoi des emails.
// =====================================================================

namespace Pressing.Models;

/// <summary>Un client du pressing (table client).</summary>
public class Client
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique du client, donné automatiquement par la base.</summary>
    public int IdClient { get; set; }

    /// <summary>Nom de famille (obligatoire).</summary>
    public string Nom { get; set; } = string.Empty;

    /// <summary>
    /// Prénom (facultatif).
    /// « string? » : le « ? » veut dire que la valeur peut être vide (null).
    /// </summary>
    public string? Prenom { get; set; }

    /// <summary>
    /// Numéro de téléphone (obligatoire). Il est UNIQUE : deux clients ne
    /// peuvent pas avoir le même numéro (RG1). C'est par lui qu'on retrouve un client.
    /// </summary>
    public string Telephone { get; set; } = string.Empty;

    /// <summary>
    /// Adresse email (OBLIGATOIRE et unique) : deux clients ne peuvent pas avoir la même.
    /// Sert à envoyer « commande prête » et « avertissement de mise en vente ».
    /// La base vérifie qu'elle a une forme valide (texte@texte.texte).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Adresse (quartier, ville) — facultative.</summary>
    public string? Adresse { get; set; }

    /// <summary>Date d'enregistrement du client (remplie par la base).</summary>
    public DateTime DateInscription { get; set; }


    // --------------------- Raccourcis (propriétés calculées) ---------------------

    /// <summary>
    /// Nom + prénom, ou seulement le nom s'il n'y a pas de prénom.
    /// Exemple : « Ngono Marie ».
    /// string.IsNullOrWhiteSpace(...) = vrai si le texte est null, vide ou que des espaces.
    /// </summary>
    public string NomComplet => string.IsNullOrWhiteSpace(Prenom) ? Nom : $"{Nom} {Prenom}";


    // ------------------------------- Affichage -------------------------------

    /// <summary>
    /// Texte affiché dans une liste ou un résultat de recherche.
    /// Exemple : « Ngono Marie - 677112233 ».
    /// </summary>
    public override string ToString() => $"{NomComplet} - {Telephone}";
}
