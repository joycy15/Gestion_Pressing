using System;
using Microsoft.Data.SqlClient;
using Pressing.Models;

namespace Pressing.DAL;

public sealed class DepotDal
{
    private readonly DbConnectionFactory _connectionFactory;

    public DepotDal(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public ResultatDepot EnregistrerDepotTransactionnel(
        CreationDepotRequest request,
        decimal montantTotal,
        decimal montantPaye,
        decimal resteAPayer,
        StatutPaiement statutPaiement)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        string statutCommandeTexte = MapStatutCommande(StatutCommande.Deposee);
        string statutPaiementTexte = MapStatutPaiement(statutPaiement);
        string? modePaiementTexte = montantPaye > 0m && request.ModePaiement.HasValue
            ? MapModePaiement(request.ModePaiement.Value)
            : null;

        using SqlConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using SqlTransaction transaction = connection.BeginTransaction();
        try
        {
            const string insertCommandeSql = @"
                INSERT INTO Commande (
                    NumeroTicket,
                    IdClient,
                    IdUtilisateur,
                    DateDepot,
                    DateRetraitPrevue,
                    DateRetraitReelle,
                    Statut,
                    MontantTotal,
                    StatutPaiement
                )
                VALUES (
                    @NumeroTicketTemporaire,
                    @IdClient,
                    @IdUtilisateur,
                    @DateDepot,
                    @DateRetraitPrevue,
                    NULL,
                    @Statut,
                    @MontantTotal,
                    @StatutPaiement
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int idCommande;
            using (SqlCommand cmdCommande = new(insertCommandeSql, connection, transaction))
            {
                cmdCommande.Parameters.AddWithValue("@NumeroTicketTemporaire", $"TMP-{Guid.NewGuid():N}"[..20]);
                cmdCommande.Parameters.AddWithValue("@IdClient", request.IdClient);
                cmdCommande.Parameters.AddWithValue("@IdUtilisateur", request.IdUtilisateur);
                cmdCommande.Parameters.AddWithValue("@DateDepot", request.DateDepot);
                cmdCommande.Parameters.AddWithValue("@DateRetraitPrevue", request.DateRetraitPrevue);
                cmdCommande.Parameters.AddWithValue("@Statut", statutCommandeTexte);
                cmdCommande.Parameters.AddWithValue("@MontantTotal", montantTotal);
                cmdCommande.Parameters.AddWithValue("@StatutPaiement", statutPaiementTexte);

                object? scalarId = cmdCommande.ExecuteScalar();
                idCommande = Convert.ToInt32(scalarId);
            }

            string numeroTicket = $"PR-{request.DateDepot.Year:0000}-{idCommande:000000}";

            const string updateTicketSql = @"
                UPDATE Commande
                SET NumeroTicket = @NumeroTicket
                WHERE IdCommande = @IdCommande;";

            using (SqlCommand cmdTicket = new(updateTicketSql, connection, transaction))
            {
                cmdTicket.Parameters.AddWithValue("@NumeroTicket", numeroTicket);
                cmdTicket.Parameters.AddWithValue("@IdCommande", idCommande);
                cmdTicket.ExecuteNonQuery();
            }

            const string insertLigneSql = @"
                INSERT INTO LigneCommande (
                    IdCommande,
                    IdTypeVetement,
                    Quantite,
                    PrixUnitaire,
                    Observation
                )
                VALUES (
                    @IdCommande,
                    @IdTypeVetement,
                    @Quantite,
                    @PrixUnitaire,
                    @Observation
                );";

            foreach (LigneDepotDto ligne in request.Lignes)
            {
                using SqlCommand cmdLigne = new(insertLigneSql, connection, transaction);
                cmdLigne.Parameters.AddWithValue("@IdCommande", idCommande);
                cmdLigne.Parameters.AddWithValue("@IdTypeVetement", ligne.IdTypeVetement);
                cmdLigne.Parameters.AddWithValue("@Quantite", ligne.Quantite);
                cmdLigne.Parameters.AddWithValue("@PrixUnitaire", ligne.PrixUnitaire);
                cmdLigne.Parameters.AddWithValue("@Observation", (object?)ligne.Observation ?? DBNull.Value);
                cmdLigne.ExecuteNonQuery();
            }

            if (montantPaye > 0m)
            {
                const string insertPaiementSql = @"
                    INSERT INTO Paiement (
                        IdCommande,
                        IdUtilisateur,
                        Montant,
                        ModePaiement,
                        Moment,
                        DatePaiement
                    )
                    VALUES (
                        @IdCommande,
                        @IdUtilisateur,
                        @Montant,
                        @ModePaiement,
                        @Moment,
                        @DatePaiement
                    );";

                using SqlCommand cmdPaiement = new(insertPaiementSql, connection, transaction);
                cmdPaiement.Parameters.AddWithValue("@IdCommande", idCommande);
                cmdPaiement.Parameters.AddWithValue("@IdUtilisateur", request.IdUtilisateur);
                cmdPaiement.Parameters.AddWithValue("@Montant", montantPaye);
                cmdPaiement.Parameters.AddWithValue("@ModePaiement", modePaiementTexte ?? "Espèces");
                cmdPaiement.Parameters.AddWithValue("@Moment", "Dépôt");
                cmdPaiement.Parameters.AddWithValue("@DatePaiement", request.DateDepot);
                cmdPaiement.ExecuteNonQuery();
            }

            transaction.Commit();

            TicketDepotDto ticket = new()
            {
                NumeroTicket = numeroTicket,
                DateDepot = request.DateDepot,
                DateRetraitPrevue = request.DateRetraitPrevue,
                NomClient = request.NomClient,
                TelephoneClient = request.TelephoneClient,
                Lignes = request.Lignes,
                MontantTotal = montantTotal,
                MontantPaye = montantPaye,
                ResteAPayer = resteAPayer,
                StatutPaiement = statutPaiement,
                ModePaiement = montantPaye > 0m ? request.ModePaiement : null
            };

            return new ResultatDepot
            {
                EstValide = true,
                IdCommande = idCommande,
                NumeroTicket = numeroTicket,
                MontantTotal = montantTotal,
                MontantPaye = montantPaye,
                ResteAPayer = resteAPayer,
                StatutPaiement = statutPaiement,
                Ticket = ticket
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string MapStatutCommande(StatutCommande statut) => statut switch
    {
        StatutCommande.Deposee => "Déposée",
        StatutCommande.Prete => "Prête",
        StatutCommande.Retiree => "Retirée",
        StatutCommande.Annulee => "Annulée",
        _ => "Déposée"
    };

    private static string MapStatutPaiement(StatutPaiement statut) => statut switch
    {
        StatutPaiement.NonPaye => "Non payé",
        StatutPaiement.PartiellementPaye => "Partiellement payé",
        StatutPaiement.Paye => "Payé",
        _ => "Non payé"
    };

    private static string MapModePaiement(ModePaiement mode) => mode switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.OrangeMoney => "Orange Money",
        ModePaiement.MtnMobileMoney => "MTN Mobile Money",
        _ => "Espèces"
    };
}
