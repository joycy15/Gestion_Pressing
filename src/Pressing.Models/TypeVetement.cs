// =====================================================================
//  TypeVetement.cs  —  Un type de vêtement accepté par le pressing
// ---------------------------------------------------------------------
//  Correspond à la table « type_vetement » de PostgreSQL.
//  Exemples : Chemise, Pantalon, Robe, Nappe, Drap.
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_type_vetement            ->   IdTypeVetement
//      libelle                     ->   Libelle
//      actif                       ->   Actif
//
//  ATTENTION : il n'y a PAS de prix ici. Le prix dépend aussi du service
//  (une chemise en express coûte plus cher qu'en standard). Le prix se
//  trouve dans la classe Tarif : (type de vêtement + service) => prix.
//
//  Utilisé par : écran Nouveau dépôt (liste des vêtements),
//  écran Paramètres (administrateur), LigneCommande.
// =====================================================================

namespace Pressing.Models;

/// <summary>Un type de vêtement (table type_vetement).</summary>
public class TypeVetement
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique du type de vêtement, donné automatiquement par la base.</summary>
    public int IdTypeVetement { get; set; }

    /// <summary>Nom du vêtement affiché (ex. « Chemise »). Unique.</summary>
    public string Libelle { get; set; } = string.Empty;

    /// <summary>
    /// false = type désactivé : il n'est plus proposé au dépôt,
    /// mais les anciennes commandes qui le contiennent restent intactes.
    /// </summary>
    public bool Actif { get; set; } = true;


    // ------------------------------- Affichage -------------------------------

    /// <summary>Texte affiché dans la liste des vêtements. Exemple : « Chemise ».</summary>
    public override string ToString() => Libelle;
}
