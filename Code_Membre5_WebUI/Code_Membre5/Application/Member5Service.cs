using Pressing.Membre5.Demo.Domain;

namespace Pressing.Membre5.Demo.Application;

public sealed class Member5Service(
    ICommandeRepository commandes,
    IPaiementRepository paiements,
    IEmailJournalRepository emails,
    INotificationRepository notifications,
    IHistoriqueStatutRepository historique,
    IEmailSender emailSender,
    IHorloge horloge,
    OptionsPressing options)
{
    public IReadOnlyList<Commande> Rechercher(string terme) => commandes.Rechercher(terme);
    public decimal ResteAPayer(Commande commande) => Math.Max(0m, commande.MontantTotal - paiements.TotalPaye(commande.Id));
    public IReadOnlyList<EmailJournal> JournalEmails() => emails.Lister();
    public IReadOnlyList<Notification> Notifications() => notifications.ListerNonTraitees();
    public void MarquerNotificationTraitee(int notificationId) => notifications.MarquerTraitee(notificationId);

    public void EnregistrerPaiementRetrait(Commande commande, decimal montant, ModePaiement mode, EmployeCourant employe)
    {
        VerifierRoleRetrait(employe);
        var reste = ResteAPayer(commande);
        if (commande.Statut is not (StatutCommande.Prete or StatutCommande.Avertie))
            throw new RegleMetierException("La commande doit être prête ou avertie pour recevoir le paiement du retrait.");
        if (montant <= 0m) throw new RegleMetierException("Le montant doit être supérieur à zéro.");
        if (montant > reste) throw new RegleMetierException($"Le montant dépasse le reste à payer ({reste:N0} FCFA).");
        paiements.Ajouter(new Paiement { CommandeId=commande.Id, EmployeId=employe.Id, Montant=montant, Mode=mode, Moment=MomentPaiement.Retrait, Date=horloge.Maintenant });
    }

    public void ValiderRetrait(Commande commande, EmployeCourant employe)
    {
        VerifierRoleRetrait(employe);
        if (commande.Statut is not (StatutCommande.Prete or StatutCommande.Avertie))
            throw new RegleMetierException("Seules les commandes prêtes ou averties peuvent être retirées.");
        if (ResteAPayer(commande) != 0m) throw new RegleMetierException("Le solde doit être entièrement payé avant le retrait.");
        ChangerStatut(commande, StatutCommande.Retiree, employe.Id);
        commande.DateRetrait = horloge.Maintenant;
        commandes.Enregistrer(commande);
    }

    public async Task SignalerCommandePreteAsync(Commande commande, CancellationToken ct = default)
    {
        if (commande.Statut != StatutCommande.Prete) throw new RegleMetierException("La commande doit déjà être au statut Prête.");
        if (commande.DatePrete is null) commande.DatePrete = horloge.Maintenant;
        commandes.Enregistrer(commande);
        if (string.IsNullOrWhiteSpace(commande.Client.Email))
        {
            notifications.Ajouter(new Notification { CommandeId=commande.Id, Titre="Client sans email", Message=$"Appeler {commande.Client.NomComplet} au {commande.Client.Telephone} pour le ticket {commande.NumeroTicket}.", Date=horloge.Maintenant });
            return;
        }
        await EnvoyerEtJournaliserAsync(commande, TypeEmail.CommandePrete, manuel:false, ct);
    }

    public async Task RenvoyerEmailPreteManuellementAsync(Commande commande, EmployeCourant employe, CancellationToken ct = default)
    {
        await RenvoyerEmailManuellementAsync(commande, TypeEmail.CommandePrete, employe, ct);
    }

    public async Task RenvoyerEmailManuellementAsync(Commande commande, TypeEmail type, EmployeCourant employe, CancellationToken ct = default)
    {
        VerifierAdmin(employe);
        if (string.IsNullOrWhiteSpace(commande.Client.Email)) throw new RegleMetierException("Le client n'a pas d'adresse email.");
        await EnvoyerEtJournaliserAsync(commande, type, manuel:true, ct);
    }

    public async Task VerifierAvertissementsAsync(CancellationToken ct = default)
    {
        foreach (var commande in commandes.ListerPretesNonRetirees())
        {
            if (commande.DatePrete is null || horloge.Maintenant < commande.DatePrete.Value.AddDays(options.JoursAvantAvertissement)) continue;
            ChangerStatut(commande, StatutCommande.Avertie, 0);
            commande.DateAvertissement = horloge.Maintenant;
            commandes.Enregistrer(commande);
            if (string.IsNullOrWhiteSpace(commande.Client.Email))
            {
                notifications.Ajouter(new Notification { CommandeId=commande.Id, Titre="Client sans email", Message=$"Appeler {commande.Client.NomComplet} au {commande.Client.Telephone}; le ticket {commande.NumeroTicket} est averti.", Date=horloge.Maintenant });
            }
            else
            {
                await EnvoyerEtJournaliserAsync(commande, TypeEmail.AvertissementVente, manuel:false, ct);
            }
        }
    }

    public IReadOnlyList<Commande> ListerVentesEligibles() => commandes.ListerVentesEligibles(horloge.Maintenant, options.JoursGraceAvantVente);

    public void MarquerVendue(Commande commande, decimal prixVente, ModePaiement mode, EmployeCourant employe)
    {
        VerifierAdmin(employe);
        if (commande.Statut != StatutCommande.Avertie) throw new RegleMetierException("Seule une commande avertie peut être vendue.");
        if (commande.DateAvertissement is null || horloge.Maintenant < commande.DateAvertissement.Value.AddDays(options.JoursGraceAvantVente))
            throw new RegleMetierException("Le délai de grâce de 7 jours n'est pas terminé.");
        if (prixVente < 0m) throw new RegleMetierException("Le prix de vente ne peut pas être négatif.");
        ChangerStatut(commande, StatutCommande.Vendue, employe.Id);
        commande.DateVente = horloge.Maintenant;
        commande.PrixVente = prixVente;
        commandes.Enregistrer(commande);
        if (prixVente > 0m)
            paiements.Ajouter(new Paiement { CommandeId=commande.Id, EmployeId=employe.Id, Montant=prixVente, Mode=mode, Moment=MomentPaiement.Vente, Date=horloge.Maintenant });
    }

    private async Task EnvoyerEtJournaliserAsync(Commande commande, TypeEmail type, bool manuel, CancellationToken ct)
    {
        if (!manuel && emails.ExisteAutomatique(commande.Id, type)) return;
        var destinataire = commande.Client.Email?.Trim();
        if (string.IsNullOrWhiteSpace(destinataire)) return;
        var (sujet, corps) = ConstruireEmail(commande, type);
        try
        {
            await emailSender.EnvoyerAsync(destinataire, sujet, corps, ct);
            emails.Ajouter(new EmailJournal { CommandeId=commande.Id, NumeroTicket=commande.NumeroTicket, Type=type, Destinataire=destinataire, Date=horloge.Maintenant, Envoye=true, Manuel=manuel });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            emails.Ajouter(new EmailJournal { CommandeId=commande.Id, NumeroTicket=commande.NumeroTicket, Type=type, Destinataire=destinataire, Date=horloge.Maintenant, Envoye=false, Erreur=ex.Message, Manuel=manuel });
            notifications.Ajouter(new Notification { CommandeId=commande.Id, Titre="Échec d'envoi email", Message=$"Échec pour le ticket {commande.NumeroTicket} ({type}). Consulter le journal puis renvoyer manuellement.", Date=horloge.Maintenant });
        }
    }

    private (string Sujet, string Corps) ConstruireEmail(Commande c, TypeEmail type)
    {
        var articles = string.Join("\n", c.Articles.Select(a => "- " + a));
        if (type == TypeEmail.CommandePrete)
            return ($"Commande {c.NumeroTicket} prête", $"Bonjour {c.Client.NomComplet},\nVotre commande {c.NumeroTicket} est prête.\nArticles :\n{articles}\nReste à payer : {ResteAPayer(c):N0} FCFA\nRetrait sous 30 jours.\n{options.NomPressing}\n{options.AdressePressing}\n{options.TelephonePressing}\n{options.Horaires}");
        var limite = c.DateAvertissement?.AddDays(options.JoursGraceAvantVente).ToString("dd/MM/yyyy") ?? "à confirmer";
        return ($"Avertissement pour {c.NumeroTicket}", $"Bonjour {c.Client.NomComplet},\nVotre commande {c.NumeroTicket} n'a pas été retirée dans les 30 jours.\nDate limite de retrait : {limite}. Après cette date, les vêtements pourront être vendus. L'avance déjà payée ne sera pas remboursée.\n{options.NomPressing} - {options.TelephonePressing}");
    }

    private static void VerifierRoleRetrait(EmployeCourant e)
    {
        if (e.Role is not (RoleEmploye.Caissier or RoleEmploye.Administrateur)) throw new RegleMetierException("Action réservée au caissier ou à l'administrateur.");
    }
    private static void VerifierAdmin(EmployeCourant e)
    {
        if (e.Role != RoleEmploye.Administrateur) throw new RegleMetierException("Action réservée à l'administrateur.");
    }

    private void ChangerStatut(Commande commande, StatutCommande nouveau, int employeId)
    {
        var ancien=commande.Statut;
        commande.Statut=nouveau;
        historique.Ajouter(new HistoriqueStatut { CommandeId=commande.Id, AncienStatut=ancien, NouveauStatut=nouveau, EmployeId=employeId, Date=horloge.Maintenant });
    }
}
