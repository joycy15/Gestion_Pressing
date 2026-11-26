// =====================================================================
//  Commande.cs  —  Un dépôt de vêtements (le cœur de l'application)
// ---------------------------------------------------------------------
//  Correspond à la table « commande » de PostgreSQL, complétée par la
//  vue « vue_commande_solde » pour les informations d'affichage
//  (nom du client, nom du service, montant déjà payé...).
//
//  Une commande = UN client + UN service (repassage seul, lavage
//  express, nettoyage à sec...) + une ou plusieurs lignes de vêtements
//  + zéro, un ou deux paiements.
//
//  Cycle de vie (colonne statut) :
//    Deposee -> EnLavage -> EnRepassage -> Prete -> Retiree
//    (repassage seul : Deposee -> EnRepassage directement)
//    Prete -> Avertie (email d'avertissement après 1 mois) -> Vendue
//    Annulee possible avant le lavage
//
//  Colonnes en base (snake_case)    ->   propriétés en C# (PascalCase)
//      id_commande                  ->   IdCommande
//      numero_ticket                ->   NumeroTicket   (ex. PR-2026-000001)
//      id_client                    ->   IdClient
//      id_service                   ->   IdService
//      id_utilisateur               ->   IdUtilisateur
//      date_depot                   ->   DateDepot
//      date_retrait_prevue          ->   DateRetraitPrevue
//      date_prete                   ->   DatePrete
//      date_avertissement           ->   DateAvertissement
//      date_retrait                 ->   DateRetrait
//      date_vente                   ->   DateVente
//      prix_vente                   ->   PrixVente
//      statut                       ->   Statut
//      montant_total                ->   MontantTotal
//      statut_paiement              ->   StatutPaiement
//
//  AUTOMATIQUE (fait par la base, jamais par le C#) :
//    - le numéro de ticket ;
//    - les dates prête / retrait / vente quand le statut change ;
//    - le montant total (somme des lignes) et le statut de paiement ;
//    - le refus des changements de statut interdits (erreur PR005)
//      et du retrait d'une commande non payée (erreur PR003).
//
//  Utilisé par : presque tous les écrans (Nouveau dépôt, file du
//  laveur, file du repasseur, Retrait, Recherche, tableau de bord).
// =====================================================================

namespace Pressing.Models;

/// <summary>Un dépôt de vêtements d'un client (table commande).</summary>
public class Commande
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de la commande, donné automatiquement par la base.</summary>
    public int IdCommande { get; set; }

    /// <summary>Numéro imprimé sur le ticket (ex. « PR-2026-000001 »). Généré par la base.</summary>
    public string NumeroTicket { get; set; } = string.Empty;

    /// <summary>Le client qui a déposé les vêtements.</summary>
    public int IdClient { get; set; }

    /// <summary>Le service choisi (il décide du circuit : laveur ou repasseur direct).</summary>
    public int IdService { get; set; }

    /// <summary>Le caissier qui a enregistré le dépôt.</summary>
    public int IdUtilisateur { get; set; }

    /// <summary>Date et heure du dépôt (remplie par la base).</summary>
    public DateTime DateDepot { get; set; }

    /// <summary>Date à laquelle le client peut revenir (dépôt + délai du service).</summary>
    public DateTime DateRetraitPrevue { get; set; }

    /// <summary>Quand la commande est devenue « Prête » (vide tant qu'elle ne l'est pas).</summary>
    public DateTime? DatePrete { get; set; }

    /// <summary>Quand l'email d'avertissement « sera vendu » a été envoyé.</summary>
    public DateTime? DateAvertissement { get; set; }

    /// <summary>Quand le client a récupéré ses vêtements.</summary>
    public DateTime? DateRetrait { get; set; }

    /// <summary>Quand les vêtements non récupérés ont été vendus.</summary>
    public DateTime? DateVente { get; set; }

    /// <summary>Prix obtenu lors de la vente (vide si pas vendue).</summary>
    public int? PrixVente { get; set; }

    /// <summary>Où en est la commande (Déposée, En lavage, Prête...).</summary>
    public StatutCommande Statut { get; set; } = StatutCommande.Deposee;


    // ------------- Calculés par la base : à LIRE seulement, jamais écrire -------------

    /// <summary>Total en FCFA = somme des lignes (recalculé par la base).</summary>
    public int MontantTotal { get; set; }

    /// <summary>Non payé / Partiellement payé / Payé (recalculé par la base).</summary>
    public StatutPaiement StatutPaiement { get; set; } = StatutPaiement.NonPaye;


    // ---------- Informations d'affichage (lues depuis vue_commande_solde) ----------
    // Elles ne sont pas des colonnes de la table commande : elles viennent
    // des tables client et service, et de la somme des paiements.

    /// <summary>Nom complet du client (affiché dans les listes et sur le ticket).</summary>
    public string NomClient { get; set; } = string.Empty;

    /// <summary>Téléphone du client.</summary>
    public string TelephoneClient { get; set; } = string.Empty;

    /// <summary>Email du client (obligatoire) : sert aux emails « prête » et « avertissement ».</summary>
    public string EmailClient { get; set; } = string.Empty;

    /// <summary>Nom du service (ex. « Nettoyage à sec »), affiché au laveur et au repasseur.</summary>
    public string NomService { get; set; } = string.Empty;

    /// <summary>Vrai pour le lavage express : la commande passe en tête de file.</summary>
    public bool Prioritaire { get; set; }

    /// <summary>Somme déjà versée par le client (tous paiements confondus).</summary>
    public int MontantPaye { get; set; }

    /// <summary>Ce qu'il reste à payer (calculé : total - déjà payé).</summary>
    public int ResteAPayer => MontantTotal - MontantPaye;


    // ------------------------- Éléments rattachés -------------------------

    /// <summary>Les vêtements déposés (au moins une ligne).</summary>
    public List<LigneCommande> Lignes { get; set; } = new();

    /// <summary>Les paiements notés (0, 1 ou 2 : au dépôt et/ou au retrait).</summary>
    public List<Paiement> Paiements { get; set; } = new();


    // ------------------------------ Méthodes ------------------------------

    /// <summary>
    /// Total calculé en C#, pour l'afficher pendant la saisie du dépôt
    /// (avant l'enregistrement). Après l'enregistrement, c'est
    /// MontantTotal, calculé par la base, qui fait foi.
    /// </summary>
    public int CalculerTotal() => Lignes.Sum(l => l.SousTotal);

    /// <summary>
    /// Vrai si la commande est finie : retirée, vendue ou annulée.
    /// Une commande terminée ne change plus de statut.
    /// </summary>
    public bool EstTerminee =>
        Statut is StatutCommande.Retiree or StatutCommande.Vendue or StatutCommande.Annulee;


    // ------------------------------- Affichage -------------------------------

    /// <summary>Exemple : « PR-2026-000001 - Mballa Paul - Prête ».</summary>
    public override string ToString() => $"{NumeroTicket} - {NomClient} - {Libelles.De(Statut)}";
}
