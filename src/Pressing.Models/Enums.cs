namespace PressingApp.Models;

// Les noms des valeurs sont exactement ceux stockés en base (colonnes NVARCHAR + CHECK).
// Conversion : valeur.ToString() pour écrire, Enum.Parse<T>(texte) pour lire.

public enum RoleUtilisateur
{
    Gerant,
    Caissier
}

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

public enum ModePaiement
{
    Especes,
    OrangeMoney,
    MobileMoney
}

public enum MomentPaiement
{
    Depot,
    Retrait
}

public enum TypeNotification
{
    Prete,
    NonRetiree
}

/// <summary>Libellés en français pour l'affichage (ComboBox, DataGridView, tickets).</summary>
public static class Libelles
{
    public static string De(RoleUtilisateur r) => r switch
    {
        RoleUtilisateur.Gerant => "Gérant",
        RoleUtilisateur.Caissier => "Caissier",
        _ => r.ToString()
    };

    public static string De(StatutCommande s) => s switch
    {
        StatutCommande.Deposee => "Déposée",
        StatutCommande.Prete => "Prête",
        StatutCommande.Retiree => "Retirée",
        StatutCommande.Annulee => "Annulée",
        _ => s.ToString()
    };

    public static string De(StatutPaiement s) => s switch
    {
        StatutPaiement.NonPaye => "Non payé",
        StatutPaiement.PartiellementPaye => "Partiellement payé",
        StatutPaiement.Paye => "Payé",
        _ => s.ToString()
    };

    public static string De(ModePaiement m) => m switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.OrangeMoney => "Orange Money",
        ModePaiement.MobileMoney => "MTN Mobile Money",
        _ => m.ToString()
    };

    public static string De(MomentPaiement m) => m switch
    {
        MomentPaiement.Depot => "Au dépôt",
        MomentPaiement.Retrait => "Au retrait",
        _ => m.ToString()
    };

    public static string De(TypeNotification t) => t switch
    {
        TypeNotification.Prete => "Commande prête",
        TypeNotification.NonRetiree => "Non retirée",
        _ => t.ToString()
    };

    /// <summary>Formate un montant en FCFA : 3500 -> "3 500 FCFA".</summary>
    public static string Fcfa(int montant) =>
        montant.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ') + " FCFA";
}
