namespace PressingApp.Models;

public class Commande
{
    public int IdCommande { get; set; }
    public string NumeroTicket { get; set; } = string.Empty;   // généré par SQL Server, lecture seule
    public int IdClient { get; set; }
    public int IdUtilisateur { get; set; }
    public DateTime DateDepot { get; set; }
    public DateTime DateRetraitPrevue { get; set; }
    public DateTime? DateRetraitReelle { get; set; }
    public StatutCommande Statut { get; set; } = StatutCommande.Deposee;

    // Calculés par les déclencheurs SQL : ne jamais les écrire depuis le C#
    public int MontantTotal { get; set; }
    public StatutPaiement StatutPaiement { get; set; } = StatutPaiement.NonPaye;

    // Champs d'affichage remplis depuis la vue VueCommandeSolde
    public string NomClient { get; set; } = string.Empty;
    public string TelephoneClient { get; set; } = string.Empty;
    public int MontantPaye { get; set; }
    public int ResteAPayer => MontantTotal - MontantPaye;

    public List<LigneCommande> Lignes { get; set; } = new();
    public List<Paiement> Paiements { get; set; } = new();

    /// <summary>Total calculé côté C# (pour l'affichage avant l'enregistrement).</summary>
    public int TotalLignes => Lignes.Sum(l => l.SousTotal);

    public override string ToString() => $"{NumeroTicket} - {NomClient} - {Libelles.De(Statut)}";
}
