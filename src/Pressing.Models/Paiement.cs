// =====================================================================
//  Paiement.cs  —  Un versement noté pour une commande
// ---------------------------------------------------------------------
//  Correspond à la table « paiement » de PostgreSQL.
//  Les paiements sont seulement NOTÉS dans l'application : il n'y a
//  aucun paiement en ligne. Le caissier encaisse (espèces, Orange Money,
//  MTN Mobile Money), puis il enregistre ce qui a été versé.
//
//  Un client peut payer :
//    - tout au dépôt              -> 1 paiement (Moment = Depot)
//    - tout au retrait            -> 1 paiement (Moment = Retrait)
//    - une avance puis le reste   -> 2 paiements (Depot + Retrait),
//                                    chacun avec son propre mode
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_paiement                 ->   IdPaiement
//      id_commande                 ->   IdCommande
//      id_utilisateur              ->   IdUtilisateur
//      montant                     ->   Montant
//      mode_paiement               ->   ModePaiement
//      moment                      ->   Moment
//      date_paiement               ->   DatePaiement
//
//  AUTOMATIQUE : après chaque paiement, la base recalcule le statut de
//  paiement de la commande (Non payé / Partiellement payé / Payé) et
//  REFUSE un paiement qui dépasserait le montant total (RG10).
//
//  Utilisé par : écran Nouveau dépôt (avance), écran Retrait (reste),
//  tableau de bord (encaissé du jour par mode).
// =====================================================================

namespace Pressing.Models;

/// <summary>Un versement noté pour une commande (table paiement).</summary>
public class Paiement
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique du paiement, donné automatiquement par la base.</summary>
    public int IdPaiement { get; set; }

    /// <summary>La commande payée.</summary>
    public int IdCommande { get; set; }

    /// <summary>L'employé (caissier ou administrateur) qui a noté le paiement.</summary>
    public int IdUtilisateur { get; set; }

    /// <summary>Somme versée en FCFA (nombre entier, toujours supérieur à 0).</summary>
    public int Montant { get; set; }

    /// <summary>Comment le client a payé : Espèces, Orange Money ou MTN Mobile Money.</summary>
    public ModePaiement ModePaiement { get; set; } = ModePaiement.Especes;

    /// <summary>Quand le client a payé : au dépôt ou au retrait.</summary>
    public MomentPaiement Moment { get; set; } = MomentPaiement.Depot;

    /// <summary>Date et heure du paiement (remplie par la base).</summary>
    public DateTime DatePaiement { get; set; }


    // ------------------------------- Affichage -------------------------------

    /// <summary>Exemple : « 2 000 FCFA - Orange Money (Au dépôt) ».</summary>
    public override string ToString() =>
        $"{Libelles.Fcfa(Montant)} - {Libelles.De(ModePaiement)} ({Libelles.De(Moment)})";
}
