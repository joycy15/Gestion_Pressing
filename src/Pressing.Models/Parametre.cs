namespace PressingApp.Models;

public class Parametre
{
    public string Cle { get; set; } = string.Empty;
    public string Valeur { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Clés utilisées par l'application
    public const string DelaiRetraitJours = "DelaiRetraitJours";
    public const string DelaiAlerteJours = "DelaiAlerteJours";
    public const string NomPressing = "NomPressing";
}
