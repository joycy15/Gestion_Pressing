using System;
using System.Collections.Generic;

namespace Pressing.Models;

public sealed class LigneDepotDto
{
    public int IdTypeVetement { get; set; }
    public string LibelleTypeVetement { get; set; } = string.Empty;
    public int Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }
    public string? Observation { get; set; }

    public decimal SousTotal => Quantite * PrixUnitaire;
}

public sealed class CreationDepotRequest
{
    public int IdClient { get; set; }
    public string NomClient { get; set; } = string.Empty;
    public string TelephoneClient { get; set; } = string.Empty;
    public int IdUtilisateur { get; set; }
    public DateTime DateDepot { get; set; } = DateTime.Now;
    public DateTime DateRetraitPrevue { get; set; }
    public List<LigneDepotDto> Lignes { get; set; } = new();
    public SituationPaiementDepot SituationPaiement { get; set; }
    public decimal MontantVerse { get; set; }
    public ModePaiement? ModePaiement { get; set; }
}

public sealed class TicketDepotDto
{
    public string NumeroTicket { get; set; } = string.Empty;
    public DateTime DateDepot { get; set; }
    public DateTime DateRetraitPrevue { get; set; }
    public string NomClient { get; set; } = string.Empty;
    public string TelephoneClient { get; set; } = string.Empty;
    public List<LigneDepotDto> Lignes { get; set; } = new();
    public decimal MontantTotal { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal ResteAPayer { get; set; }
    public StatutPaiement StatutPaiement { get; set; }
    public ModePaiement? ModePaiement { get; set; }
}

public sealed class ResultatDepot
{
    public bool EstValide { get; set; }
    public List<string> Erreurs { get; set; } = new();
    public int IdCommande { get; set; }
    public string NumeroTicket { get; set; } = string.Empty;
    public decimal MontantTotal { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal ResteAPayer { get; set; }
    public StatutPaiement StatutPaiement { get; set; }
    public TicketDepotDto? Ticket { get; set; }
}
