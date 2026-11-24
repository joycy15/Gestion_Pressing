namespace PressingApp.Models;

public class Utilisateur
{
    public int IdUtilisateur { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public string Sel { get; set; } = string.Empty;
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Caissier;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }

    public bool EstGerant => Role == RoleUtilisateur.Gerant;

    public override string ToString() => $"{Nom} ({Libelles.De(Role)})";
}
