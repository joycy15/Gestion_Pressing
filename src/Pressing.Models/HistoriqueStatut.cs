// =====================================================================
//  HistoriqueStatut.cs  —  Une ligne du journal des statuts
// ---------------------------------------------------------------------
//  Correspond à la table « historique_statut » de PostgreSQL.
//  Chaque fois qu'une commande change de statut (ex. Déposée -> En
//  lavage), la base ajoute AUTOMATIQUEMENT une ligne dans ce journal :
//  ancien statut, nouveau statut, qui l'a fait et quand.
//
//  Le C# ne crée JAMAIS ces lignes : il se contente de les LIRE pour
//  afficher l'historique d'une commande (traçabilité).
//
//  Comment la base sait « qui » a fait le changement ?
//    Avant chaque modification, la DAL exécute :
//        SELECT set_config('app.id_utilisateur', '<id>', true);
//    et le trigger lit cette valeur avec fn_utilisateur_courant().
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_historique               ->   IdHistorique
//      id_commande                 ->   IdCommande
//      ancien_statut               ->   AncienStatut   (vide à la création)
//      nouveau_statut              ->   NouveauStatut
//      id_utilisateur              ->   IdUtilisateur  (peut être vide)
//      date_changement             ->   DateChangement
//
//  Utilisé par : écran Détail / Recherche de commande (onglet
//  « Historique »), et l'administrateur pour vérifier qui a fait quoi.
// =====================================================================

namespace Pressing.Models;

/// <summary>Un changement de statut d'une commande (table historique_statut, remplie par la base).</summary>
public class HistoriqueStatut
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de la ligne d'historique.</summary>
    public int IdHistorique { get; set; }

    /// <summary>La commande concernée.</summary>
    public int IdCommande { get; set; }

    /// <summary>Statut avant le changement. Vide (null) pour la création de la commande.</summary>
    public StatutCommande? AncienStatut { get; set; }

    /// <summary>Statut après le changement.</summary>
    public StatutCommande NouveauStatut { get; set; }

    /// <summary>L'employé qui a fait le changement (vide si fait par le système, ex. vente automatique).</summary>
    public int? IdUtilisateur { get; set; }

    /// <summary>Date et heure du changement.</summary>
    public DateTime DateChangement { get; set; }


    // ------------------------- Affichage -------------------------

    /// <summary>Nom de l'employé (lu par jointure avec la table utilisateur).</summary>
    public string NomUtilisateur { get; set; } = string.Empty;

    /// <summary>
    /// Exemple : « 26/11/2026 14:05 : Déposée -> En lavage (Laveur 1) ».
    /// À la création : « 26/11/2026 09:00 : Création -> Déposée (Caissier 1) ».
    /// </summary>
    public override string ToString()
    {
        // Si AncienStatut est vide, c'est la création de la commande
        string avant = AncienStatut is null ? "Création" : Libelles.De(AncienStatut.Value);

        // Si personne n'est connu, c'est le système qui a agi
        string qui = string.IsNullOrEmpty(NomUtilisateur) ? "Système" : NomUtilisateur;

        return $"{DateChangement:dd/MM/yyyy HH:mm} : {avant} -> {Libelles.De(NouveauStatut)} ({qui})";
    }
}
