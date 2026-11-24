namespace PressingApp.Models;

public class LigneCommande
{
    public int IdLigne { get; set; }
    public int IdCommande { get; set; }
    public int IdTypeVetement { get; set; }
    public int Quantite { get; set; } = 1;
    public int PrixUnitaire { get; set; }      // copié depuis TypeVetement au moment du dépôt
    public string? Observation { get; set; }

    // Affichage
    public string LibelleVetement { get; set; } = string.Empty;

    public int SousTotal => Quantite * PrixUnitaire;
}
