using System;
using System.Collections.Generic;
using System.Linq;
using Pressing.Models;

namespace Pressing.BLL;

public sealed class DepotCalculator
{
    public decimal CalculerMontantTotal(IEnumerable<LigneDepotDto>? lignes)
    {
        if (lignes is null)
        {
            return 0m;
        }

        return lignes.Sum(l => l.Quantite * l.PrixUnitaire);
    }

    public decimal CalculerMontantPaye(CreationDepotRequest request, decimal montantTotal)
    {
        if (montantTotal <= 0m)
        {
            return 0m;
        }

        return request.SituationPaiement switch
        {
            SituationPaiementDepot.PayeEnTotalite => montantTotal,
            SituationPaiementDepot.PayeEnPartie => request.MontantVerse,
            SituationPaiementDepot.APayerAuRetrait => 0m,
            _ => 0m
        };
    }

    public decimal CalculerResteAPayer(decimal montantTotal, decimal montantPaye)
    {
        decimal reste = montantTotal - montantPaye;
        return reste < 0m ? 0m : reste;
    }

    public StatutPaiement DeterminerStatutPaiement(decimal montantTotal, decimal montantPaye)
    {
        decimal reste = CalculerResteAPayer(montantTotal, montantPaye);

        if (montantTotal > 0m && reste == 0m)
        {
            return StatutPaiement.Paye;
        }

        if (montantPaye > 0m && reste > 0m)
        {
            return StatutPaiement.PartiellementPaye;
        }

        return StatutPaiement.NonPaye;
    }
}

public sealed class DepotValidator
{
    public List<string> Valider(CreationDepotRequest? request, decimal montantTotal)
    {
        List<string> erreurs = new();

        if (request is null)
        {
            erreurs.Add("Les informations du dépôt sont manquantes.");
            return erreurs;
        }

        // RG2 : Une commande appartient à un seul client et contient au moins une ligne.
        if (request.IdClient <= 0)
        {
            erreurs.Add("Veuillez sélectionner ou créer un client valide (RG2).");
        }

        if (request.IdUtilisateur <= 0)
        {
            erreurs.Add("Utilisateur connecté invalide pour l'enregistrement du dépôt.");
        }

        if (request.Lignes is null || request.Lignes.Count == 0)
        {
            erreurs.Add("La commande doit contenir au moins une ligne de vêtement (RG2).");
        }
        else
        {
            for (int i = 0; i < request.Lignes.Count; i++)
            {
                LigneDepotDto ligne = request.Lignes[i];
                if (ligne.IdTypeVetement <= 0)
                {
                    erreurs.Add($"Ligne {i + 1} : le type de vêtement est obligatoire.");
                }

                if (ligne.Quantite <= 0)
                {
                    erreurs.Add($"Ligne {i + 1} : la quantité doit être supérieure à 0.");
                }

                if (ligne.PrixUnitaire < 0m)
                {
                    erreurs.Add($"Ligne {i + 1} : le prix unitaire ne peut pas être négatif.");
                }
            }
        }

        // RG6 : Date de retrait prévue cohérente avec la date de dépôt.
        if (request.DateRetraitPrevue.Date < request.DateDepot.Date)
        {
            erreurs.Add("La date de retrait prévue ne peut pas être antérieure à la date de dépôt (RG6).");
        }

        // RG4 : Montant total valide.
        if (montantTotal <= 0m)
        {
            erreurs.Add("Le montant total de la commande doit être supérieur à 0 FCFA (RG4).");
        }

        // RG7, RG8, RG9 : Situation, mode et plafond de paiement.
        switch (request.SituationPaiement)
        {
            case SituationPaiementDepot.PayeEnTotalite:
                if (!request.ModePaiement.HasValue)
                {
                    erreurs.Add("Veuillez choisir un mode de paiement pour un règlement total (RG8).");
                }
                break;

            case SituationPaiementDepot.PayeEnPartie:
                if (request.MontantVerse <= 0m)
                {
                    erreurs.Add("Veuillez saisir une avance supérieure à 0 FCFA pour un paiement partiel.");
                }
                else if (request.MontantVerse >= montantTotal)
                {
                    erreurs.Add("Pour un paiement partiel, l'avance doit être inférieure au montant total (choisissez 'Payé en totalité' si le montant couvre tout, RG9).");
                }

                if (!request.ModePaiement.HasValue)
                {
                    erreurs.Add("Tout montant versé doit avoir un mode de paiement : Espèces, Orange Money ou MTN Mobile Money (RG8).");
                }
                break;

            case SituationPaiementDepot.APayerAuRetrait:
                if (request.MontantVerse > 0m)
                {
                    erreurs.Add("Aucun montant ne doit être saisi au dépôt lorsque la situation est 'À payer au retrait'.");
                }
                break;

            default:
                erreurs.Add("La situation du paiement au dépôt est obligatoire (RG7).");
                break;
        }

        if (request.MontantVerse < 0m)
        {
            erreurs.Add("Le montant versé ne peut pas être négatif.");
        }

        if (request.MontantVerse > montantTotal)
        {
            erreurs.Add("Le total des paiements ne peut pas dépasser le montant total de la commande (RG9).");
        }

        return erreurs;
    }
}

public sealed class DepotService
{
    private readonly DepotCalculator _calculator;
    private readonly DepotValidator _validator;

    public DepotService()
        : this(new DepotCalculator(), new DepotValidator())
    {
    }

    public DepotService(DepotCalculator calculator, DepotValidator validator)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public ResultatDepot PreparerEtValider(CreationDepotRequest? request)
    {
        decimal montantTotal = _calculator.CalculerMontantTotal(request?.Lignes);
        List<string> erreurs = _validator.Valider(request, montantTotal);

        if (erreurs.Count > 0 || request is null)
        {
            return new ResultatDepot
            {
                EstValide = false,
                Erreurs = erreurs,
                MontantTotal = montantTotal
            };
        }

        decimal montantPaye = _calculator.CalculerMontantPaye(request, montantTotal);
        decimal resteAPayer = _calculator.CalculerResteAPayer(montantTotal, montantPaye);
        StatutPaiement statutPaiement = _calculator.DeterminerStatutPaiement(montantTotal, montantPaye);

        List<LigneDepotDto> lignesCopie = request.Lignes
            .Select(l => new LigneDepotDto
            {
                IdTypeVetement = l.IdTypeVetement,
                LibelleTypeVetement = l.LibelleTypeVetement,
                Quantite = l.Quantite,
                PrixUnitaire = l.PrixUnitaire,
                Observation = l.Observation
            })
            .ToList();

        return new ResultatDepot
        {
            EstValide = true,
            MontantTotal = montantTotal,
            MontantPaye = montantPaye,
            ResteAPayer = resteAPayer,
            StatutPaiement = statutPaiement,
            Ticket = new TicketDepotDto
            {
                DateDepot = request.DateDepot,
                DateRetraitPrevue = request.DateRetraitPrevue,
                NomClient = request.NomClient,
                TelephoneClient = request.TelephoneClient,
                Lignes = lignesCopie,
                MontantTotal = montantTotal,
                MontantPaye = montantPaye,
                ResteAPayer = resteAPayer,
                StatutPaiement = statutPaiement,
                ModePaiement = montantPaye > 0m ? request.ModePaiement : null
            }
        };
    }
}
