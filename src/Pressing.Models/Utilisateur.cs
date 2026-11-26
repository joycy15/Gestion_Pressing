// =====================================================================
//  Utilisateur.cs  —  Un employé du pressing
// ---------------------------------------------------------------------
//  Correspond à la table « utilisateur » de PostgreSQL.
//  Une ligne de la table  <=>  un objet Utilisateur en C#.
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_utilisateur              ->   IdUtilisateur
//      nom                         ->   Nom
//      login                       ->   Login
//      mot_de_passe_hash           ->   MotDePasseHash
//      sel                         ->   Sel
//      role                        ->   Role
//      actif                       ->   Actif
//      date_creation               ->   DateCreation
//
//  Utilisé par : écran de connexion, menu selon le rôle,
//  écran de gestion des utilisateurs (administrateur).
// =====================================================================

namespace Pressing.Models;

/// <summary>Un employé du pressing (table utilisateur).</summary>
public class Utilisateur
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de l'employé, donné automatiquement par la base.</summary>
    public int IdUtilisateur { get; set; }

    /// <summary>Nom affiché dans l'application (ex. « Caissier 1 »).</summary>
    public string Nom { get; set; } = string.Empty;   // string.Empty = texte vide au départ (jamais null)

    /// <summary>Identifiant tapé à l'écran de connexion (ex. « caissier1 »). Unique.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>
    /// Mot de passe HACHÉ (illisible), jamais le vrai mot de passe.
    /// Le hachage est fait par PasswordHasher (projet Pressing.BLL).
    /// </summary>
    public string MotDePasseHash { get; set; } = string.Empty;

    /// <summary>« Sel » aléatoire utilisé pour hacher le mot de passe (rend le hachage unique).</summary>
    public string Sel { get; set; } = string.Empty;

    /// <summary>
    /// Rôle de l'employé : Administrateur, Caissier, Laveur ou Repasseur (voir Enums.cs).
    /// Par défaut, un nouvel employé est Caissier.
    /// </summary>
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Caissier;

    /// <summary>
    /// false = compte désactivé : l'employé ne peut plus se connecter.
    /// On désactive au lieu de supprimer, pour garder l'historique (RG18).
    /// </summary>
    public bool Actif { get; set; } = true;

    /// <summary>Date de création du compte (remplie par la base).</summary>
    public DateTime DateCreation { get; set; }


    // --------------------- Raccourcis (propriétés calculées) ---------------------
    // Ils ne sont pas stockés en base : ils sont calculés à partir de Role.

    /// <summary>true si l'employé est l'administrateur (le gérant).</summary>
    public bool EstAdministrateur => Role == RoleUtilisateur.Administrateur;

    /// <summary>
    /// true si l'employé peut voir les écrans de l'atelier (lavage, repassage).
    /// L'administrateur peut tout faire, donc il en fait partie.
    /// </summary>
    public bool PeutTravaillerALAtelier =>
        Role is RoleUtilisateur.Laveur or RoleUtilisateur.Repasseur or RoleUtilisateur.Administrateur;


    // ------------------------------- Affichage -------------------------------

    /// <summary>
    /// Texte affiché quand on met un Utilisateur dans une liste déroulante.
    /// Exemple : « Laveur 1 (Laveur) ».
    /// </summary>
    public override string ToString() => $"{Nom} ({Libelles.De(Role)})";
}
