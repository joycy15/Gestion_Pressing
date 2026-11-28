namespace Pressing.Membre5.Demo.Domain;

// Types du module Membre 5. La conversion vers/depuis les modèles communs (Pressing.Models)
// et la base PostgreSQL est faite dans Infrastructure/PostgresRepositories.cs.
public enum StatutCommande { Deposee, EnLavage, EnRepassage, Prete, Avertie, Retiree, Vendue, Annulee, EnLivraison, Livree }
public enum ModePaiement { Especes, OrangeMoney, MtnMobileMoney }
public enum MomentPaiement { Depot, Retrait, Vente, Livraison }
public enum TypeEmail { CommandePrete, AvertissementVente }
public enum RoleEmploye { Administrateur, Caissier, Laveur, Repasseur, Livreur }

public sealed class Client
{
    public int Id { get; set; }
    public string NomComplet { get; set; } = "";
    public string Telephone { get; set; } = "";
    public string? Email { get; set; }
}

public sealed class Commande
{
    public int Id { get; set; }
    public string NumeroTicket { get; set; } = "";
    public Client Client { get; set; } = new();
    public string Service { get; set; } = "";
    public List<string> Articles { get; set; } = new();
    public decimal MontantTotal { get; set; }
    public StatutCommande Statut { get; set; }
    public DateTimeOffset DateDepot { get; set; }
    public DateTimeOffset? DatePrete { get; set; }
    public DateTimeOffset? DateAvertissement { get; set; }
    public DateTimeOffset? DateRetrait { get; set; }
    public DateTimeOffset? DateVente { get; set; }
    public decimal? PrixVente { get; set; }
}

public sealed class Paiement
{
    public int Id { get; set; }
    public int CommandeId { get; set; }
    public int EmployeId { get; set; }
    public decimal Montant { get; set; }
    public ModePaiement Mode { get; set; }
    public MomentPaiement Moment { get; set; }
    public DateTimeOffset Date { get; set; }
}

public sealed class Notification
{
    public int Id { get; set; }
    public int? CommandeId { get; set; }
    public string Titre { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public bool Traitee { get; set; }
}

public sealed class HistoriqueStatut
{
    public int CommandeId { get; set; }
    public StatutCommande AncienStatut { get; set; }
    public StatutCommande NouveauStatut { get; set; }
    public int EmployeId { get; set; }
    public DateTimeOffset Date { get; set; }
}

public sealed class EmailJournal
{
    public int Id { get; set; }
    public int CommandeId { get; set; }
    public string NumeroTicket { get; set; } = "";
    public TypeEmail Type { get; set; }
    public string Destinataire { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public bool Envoye { get; set; }
    public string? Erreur { get; set; }
    public bool Manuel { get; set; }
}

public sealed class EmployeCourant
{
    public int Id { get; set; } = 1;
    public RoleEmploye Role { get; set; } = RoleEmploye.Administrateur;
}

public sealed class RegleMetierException(string message) : Exception(message) { }
