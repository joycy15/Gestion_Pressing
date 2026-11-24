namespace PressingApp.Models;

public class Notification
{
    public int IdNotification { get; set; }
    public int IdCommande { get; set; }
    public TypeNotification Type { get; set; }
    public DateTime DateCreation { get; set; }
    public bool Traitee { get; set; }
    public DateTime? DateTraitement { get; set; }

    // Affichage (jointure avec Commande et Client)
    public string NumeroTicket { get; set; } = string.Empty;
    public string NomClient { get; set; } = string.Empty;
    public string TelephoneClient { get; set; } = string.Empty;
}
