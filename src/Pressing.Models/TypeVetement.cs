namespace PressingApp.Models;

public class TypeVetement
{
    public int IdTypeVetement { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public int PrixUnitaire { get; set; }          // FCFA
    public bool Actif { get; set; } = true;

    public override string ToString() => $"{Libelle} - {Libelles.Fcfa(PrixUnitaire)}";
}
