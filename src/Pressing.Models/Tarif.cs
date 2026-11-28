// =====================================================================
//  Tarif.cs  —  Un prix de la grille : (type de vêtement + service) => prix
// ---------------------------------------------------------------------
//  Correspond à la table « tarif » de PostgreSQL (et à la vue vue_tarifs
//  pour les noms affichés).
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_type_vetement            ->   IdTypeVetement
//      id_service                  ->   IdService
//      prix                        ->   Prix
//
//  Il n'y a pas de numéro « id_tarif » : un tarif est identifié par le
//  COUPLE (vêtement, service). La base interdit deux prix pour le même couple.
//
//  Exemple pour « Chemise / chemisette » :
//      Repassage seul       200 FCFA   (40 % du prix de la grille)
//      Lavage + repassage   500 FCFA   (prix de la grille officielle)
//      Lavage express       650 FCFA   (+ 30 %)
//      Nettoyage à sec    1 000 FCFA   (× 2)
//
//  Au dépôt, le prix trouvé ici est COPIÉ dans LigneCommande.PrixUnitaire :
//  si l'administrateur change un prix plus tard, les anciennes commandes
//  gardent le prix du jour du dépôt (RG4).
//
//  Utilisé par : écran Nouveau dépôt (prix de chaque vêtement),
//  écran Paramètres (grille de tarifs modifiable par l'administrateur).
// =====================================================================

namespace Pressing.Models;

/// <summary>Le prix d'un type de vêtement pour un service (table tarif).</summary>
public class Tarif
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Le vêtement concerné (lien vers TypeVetement).</summary>
    public int IdTypeVetement { get; set; }

    /// <summary>Le service concerné (lien vers Service).</summary>
    public int IdService { get; set; }

    /// <summary>Prix en FCFA (nombre entier, toujours supérieur à 0).</summary>
    public int Prix { get; set; }


    // ------------------ Affichage (remplis depuis vue_tarifs) ------------------
    // Ces deux textes ne sont pas dans la table tarif : la vue vue_tarifs les
    // ajoute en allant chercher les noms dans type_vetement et service.

    /// <summary>Nom du vêtement (ex. « Chemise / chemisette »).</summary>
    public string NomVetement { get; set; } = string.Empty;

    /// <summary>Nom du service (ex. « Lavage express »).</summary>
    public string NomService { get; set; } = string.Empty;


    // ------------------------------- Affichage -------------------------------

    /// <summary>Exemple : « Chemise / chemisette - Lavage express : 650 FCFA ».</summary>
    public override string ToString() => $"{NomVetement} - {NomService} : {Libelles.Fcfa(Prix)}";
}
