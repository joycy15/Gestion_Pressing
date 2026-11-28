using Pressing.Membre5.Demo.Application;
using Pressing.Membre5.Demo.Infrastructure;
using Pressing.Membre5.Demo.WebUI;

namespace Pressing.Membre5.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Les valeurs de OptionsPressing sont lues dans la table « parametre » après la connexion (voir WebBridge).
        var options = new OptionsPressing();
        var commandes = new PostgresCommandeRepository();
        var service = new Member5Service(
            commandes,
            new PostgresPaiementRepository(),
            new PostgresEmailJournalRepository(),
            new PostgresNotificationRepository(),
            new PostgresHistoriqueRepository(),
            new ParametresSmtpEmailSender(),
            new HorlogeSysteme(),
            options);

        System.Windows.Forms.Application.Run(new WebShellForm(service, commandes, options));
    }
}
