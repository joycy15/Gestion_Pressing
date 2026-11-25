namespace Pressing.Models;

public enum StatutCommande
{
    Deposee,
    Prete,
    Retiree,
    Annulee
}

public enum StatutPaiement
{
    NonPaye,
    PartiellementPaye,
    Paye
}

public enum SituationPaiementDepot
{
    PayeEnTotalite,
    PayeEnPartie,
    APayerAuRetrait
}

public enum ModePaiement
{
    Especes,
    OrangeMoney,
    MtnMobileMoney
}

public enum MomentPaiement
{
    Depot,
    Retrait
}
