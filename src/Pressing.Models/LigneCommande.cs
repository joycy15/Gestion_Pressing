// =====================================================================
//  LigneCommande.cs  —  Une ligne d'une commande (un type de vêtement)
// ---------------------------------------------------------------------
//  Correspond à la table « ligne_commande » de PostgreSQL.
//  Une commande « 3 chemises + 2 pantalons » contient 2 lignes :
//      ligne 1 : Chemise / chemisette, quantité 3, prix 500
//      ligne 2 : Pantalon,             quantité 2, prix 500
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_ligne                    ->   IdLigne
//      id_commande                 ->   IdCommande
//      id_type_vetement            ->   IdTypeVetement
//      quantite                    ->   Quantite
//      prix_unitaire               ->   PrixUnitaire
//      observation                 ->   Observation   (facultative)
//
//  AUTOMATIQUE : quand on ajoute / modifie / supprime une ligne, la base
//  recalcule le montant total de la commande et son statut de paiement.
//
//  Utilisé par : écran Nouveau dépôt, écrans Lavage / Repassage / Retrait,
//  ticket imprimé.
// =====================================================================

namespace Pressing.Models;

/// <summary>Une ligne d'une commande (table ligne_commande).</summary>
public class LigneCommande
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de la ligne, donné automatiquement par la base.</summary>
    public int IdLigne { get; set; }

    /// <summary>La commande à laquelle appartient cette ligne.</summary>
    public int IdCommande { get; set; }

    /// <summary>Le type de vêtement (lien vers TypeVetement).</summary>
    public int IdTypeVetement { get; set; }

    /// <summary>Nombre de pièces de ce type (au moins 1). Par défaut : 1.</summary>
    public int Quantite { get; set; } = 1;

    /// <summary>
    /// Prix d'UNE pièce en FCFA, COPIÉ de la grille Tarif au moment du dépôt.
    /// Ainsi, si le prix change plus tard, cette commande garde son prix (RG4).
    /// </summary>
    public int PrixUnitaire { get; set; }

    /// <summary>
    /// Remarque pour l'atelier (facultative) : « tache au col »,
    /// « couleur fragile », « bouton manquant »…
    /// </summary>
    public string? Observation { get; set; }


    // --------------------- Affichage (pas dans la table) ---------------------

    /// <summary>
    /// Nom du vêtement, rempli en lisant la commande (jointure avec type_vetement).
    /// Sert à afficher « Chemise / chemisette » au lieu du numéro.
    /// </summary>
    public string LibelleVetement { get; set; } = string.Empty;


    // --------------------- Calcul (propriété calculée) ---------------------

    /// <summary>
    /// Montant de la ligne = quantité × prix unitaire.
    /// Exemple : 3 chemises × 500 = 1 500 FCFA.
    /// </summary>
    public int SousTotal => Quantite * PrixUnitaire;


    // ------------------------------- Affichage -------------------------------

    /// <summary>Exemple : « 3 × Chemise / chemisette ».</summary>
    public override string ToString() => $"{Quantite} × {LibelleVetement}";
}
