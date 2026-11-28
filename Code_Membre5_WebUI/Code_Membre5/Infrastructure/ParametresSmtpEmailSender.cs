using Pressing.DAL;
using Pressing.Membre5.Demo.Application;
using M = Pressing.Models;

namespace Pressing.Membre5.Demo.Infrastructure;

// Envoi réel par SMTP (MailKit). Serveur, port et expéditeur viennent de la table « parametre » ;
// le mot de passe du compte email vient UNIQUEMENT de appsettings.json (Email > MotDePasse).
// Les paramètres sont relus à chaque envoi : l'administrateur peut les changer sans recompiler.
public sealed class ParametresSmtpEmailSender : IEmailSender
{
    public async Task EnvoyerAsync(string destinataire, string sujet, string corps, CancellationToken ct = default)
    {
        int port = ParametreRepository.LireEntier(M.Parametre.SmtpPort, 587);
        var configuration = new ConfigurationSmtp
        {
            Hote = ParametreRepository.Lire(M.Parametre.SmtpServeur) ?? "",
            Port = port,
            AdresseExpediteur = ParametreRepository.Lire(M.Parametre.EmailExpediteur) ?? "",
            MotDePasseApplication = Configuration.MotDePasseEmail,
            UtiliserSslDirect = port == 465
        };
        if (string.IsNullOrWhiteSpace(configuration.Hote) || string.IsNullOrWhiteSpace(configuration.AdresseExpediteur))
            throw new InvalidOperationException("SmtpServeur ou EmailExpediteur est vide dans la table parametre.");

        await new MailKitEmailSender(configuration).EnvoyerAsync(destinataire, sujet, corps, ct);
    }
}
