namespace PressingApp.Models;

public class Paiement
{
    public int IdPaiement { get; set; }
    public int IdCommande { get; set; }
    public int IdUtilisateur { get; set; }
    public int Montant { get; set; }           // FCFA, > 0
    public ModePaiement ModePaiement { get; set; } = ModePaiement.Especes;
    public MomentPaiement Moment { get; set; } = MomentPaiement.Depot;
    public DateTime DatePaiement { get; set; }
}
