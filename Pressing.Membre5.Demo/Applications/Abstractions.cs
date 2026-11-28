using Pressing.Membre5.Demo.Domain;

namespace Pressing.Membre5.Demo.Application;

public interface ICommandeRepository
{
    Commande? TrouverParTicket(string ticket);
    IReadOnlyList<Commande> Rechercher(string terme);
    IReadOnlyList<Commande> ListerPretesNonRetirees();
    IReadOnlyList<Commande> ListerVentesEligibles(DateTimeOffset maintenant, int joursGrace);
    void Enregistrer(Commande commande);
}

public interface IPaiementRepository
{
    decimal TotalPaye(int commandeId);
    IReadOnlyList<Paiement> ListerPourCommande(int commandeId);
    void Ajouter(Paiement paiement);
}

public interface IEmailJournalRepository
{
    bool ExisteAutomatique(int commandeId, TypeEmail type);
    void Ajouter(EmailJournal entree);
    IReadOnlyList<EmailJournal> Lister();
}

public interface INotificationRepository
{
    void Ajouter(Notification notification);
    IReadOnlyList<Notification> ListerNonTraitees();
    void MarquerTraitee(int notificationId);
}

public interface IHistoriqueStatutRepository
{
    void Ajouter(HistoriqueStatut entree);
}

public interface IEmailSender
{
    Task EnvoyerAsync(string destinataire, string sujet, string corps, CancellationToken ct = default);
}

public interface IHorloge { DateTimeOffset Maintenant { get; } }
public sealed class HorlogeSysteme : IHorloge { public DateTimeOffset Maintenant => DateTimeOffset.Now; }

public sealed class OptionsPressing
{
    public int JoursAvantAvertissement { get; set; } = 30;
    public int JoursGraceAvantVente { get; set; } = 7;
    public string NomPressing { get; set; } = "PressiGest";
    public string AdressePressing { get; set; } = "Adresse du pressing";
    public string TelephonePressing { get; set; } = "Téléphone du pressing";
    public string Horaires { get; set; } = "Horaires à compléter";
}
