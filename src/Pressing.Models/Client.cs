namespace PressingApp.Models;

public class Client
{
    public int IdClient { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Prenom { get; set; }
    public string Telephone { get; set; } = string.Empty;
    public string? Adresse { get; set; }
    public DateTime DateInscription { get; set; }

    public string NomComplet => string.IsNullOrWhiteSpace(Prenom) ? Nom : $"{Nom} {Prenom}";

    public override string ToString() => $"{NomComplet} - {Telephone}";
}
