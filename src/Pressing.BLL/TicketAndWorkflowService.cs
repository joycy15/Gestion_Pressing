using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Pressing.Models;

namespace Pressing.BLL;

public sealed class TicketDocumentFormatter
{
    public string FormaterTexteTicket(TicketDepotDto ticket)
    {
        if (ticket is null)
        {
            throw new ArgumentNullException(nameof(ticket));
        }

        CultureInfo culture = CultureInfo.GetCultureInfo("fr-FR");
        StringBuilder sb = new();

        sb.AppendLine("========================================");
        sb.AppendLine("         GESTION PRESSING - TICKET");
        sb.AppendLine("========================================");
        sb.AppendLine($"N° Ticket      : {ticket.NumeroTicket}");
        sb.AppendLine($"Date dépôt     : {ticket.DateDepot.ToString("dd/MM/yyyy HH:mm", culture)}");
        sb.AppendLine($"Retrait prévu  : {ticket.DateRetraitPrevue.ToString("dd/MM/yyyy", culture)}");
        sb.AppendLine($"Client         : {ticket.NomClient}");
        sb.AppendLine($"Téléphone      : {ticket.TelephoneClient}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("ARTICLES DEPOSES :");

        foreach (LigneDepotDto ligne in ticket.Lignes)
        {
            sb.AppendLine($"- {ligne.Quantite} x {ligne.LibelleTypeVetement} ({ligne.PrixUnitaire:N0} FCFA) = {ligne.SousTotal:N0} FCFA");
            if (!string.IsNullOrWhiteSpace(ligne.Observation))
            {
                sb.AppendLine($"  Obs. : {ligne.Observation.Trim()}");
            }
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Montant total  : {ticket.MontantTotal:N0} FCFA");
        sb.AppendLine($"Montant payé   : {ticket.MontantPaye:N0} FCFA");
        sb.AppendLine($"Reste à payer  : {ticket.ResteAPayer:N0} FCFA");
        sb.AppendLine($"Statut paie.   : {FormaterStatutPaiement(ticket.StatutPaiement)}");

        if (ticket.MontantPaye > 0m && ticket.ModePaiement.HasValue)
        {
            sb.AppendLine($"Mode paiement  : {FormaterModePaiement(ticket.ModePaiement.Value)}");
        }

        sb.AppendLine("========================================");
        sb.AppendLine("   Merci de conserver ce ticket.");
        sb.AppendLine("   Vérifiez vos articles au retrait.");
        sb.AppendLine("========================================");

        return sb.ToString();
    }

    private static string FormaterStatutPaiement(StatutPaiement statut) => statut switch
    {
        StatutPaiement.Paye => "Payé",
        StatutPaiement.PartiellementPaye => "Partiellement payé",
        StatutPaiement.NonPaye => "Non payé",
        _ => "Non payé"
    };

    private static string FormaterModePaiement(ModePaiement mode) => mode switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.OrangeMoney => "Orange Money",
        ModePaiement.MtnMobileMoney => "MTN Mobile Money",
        _ => "Espèces"
    };
}

public sealed class DepotWorkflowService
{
    private readonly DepotService _depotService;
    private readonly DepotDal _depotDal;

    public DepotWorkflowService(DepotService depotService, DepotDal depotDal)
    {
        _depotService = depotService ?? throw new ArgumentNullException(nameof(depotService));
        _depotDal = depotDal ?? throw new ArgumentNullException(nameof(depotDal));
    }

    public ResultatDepot CreerDepot(CreationDepotRequest? request)
    {
        ResultatDepot preparation = _depotService.PreparerEtValider(request);
        if (!preparation.EstValide || request is null)
        {
            return preparation;
        }

        return _depotDal.EnregistrerDepotTransactionnel(
            request,
            preparation.MontantTotal,
            preparation.MontantPaye,
            preparation.ResteAPayer,
            preparation.StatutPaiement);
    }
}
