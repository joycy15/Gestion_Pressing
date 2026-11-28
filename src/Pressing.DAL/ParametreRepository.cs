// =====================================================================
//  ParametreRepository.cs  —  Requêtes sur la table parametre
// ---------------------------------------------------------------------
//  Lire et modifier les réglages du pressing (délais, nom, adresse,
//  serveur SMTP...). Les noms des clés sont des constantes de la classe
//  Parametre (Pressing.Models) :
//
//      int delai = ParametreRepository.LireEntier(Parametre.DelaiGraceJours, 7);
//      string nom = ParametreRepository.Lire(Parametre.NomPressing) ?? "PressiGest";
//
//  Rappel sécurité : AUCUN mot de passe dans cette table. Le mot de passe
//  email est dans appsettings.json (voir Configuration.MotDePasseEmail).
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table parametre.</summary>
public static class ParametreRepository
{
    /// <summary>Tous les réglages (écran Paramètres de l'administrateur).</summary>
    public static List<Parametre> ListerTous() =>
        Database.LireListe("SELECT cle, valeur, description FROM parametre ORDER BY cle", VersParametre);

    /// <summary>Valeur texte d'un réglage, ou null si la clé n'existe pas.</summary>
    public static string? Lire(string cle) =>
        Database.Scalaire("SELECT valeur FROM parametre WHERE cle = @cle", Database.P("@cle", cle)) as string;

    /// <summary>Valeur entière d'un réglage ; valeurParDefaut si absent ou pas un nombre.</summary>
    public static int LireEntier(string cle, int valeurParDefaut) =>
        int.TryParse(Lire(cle), out int nombre) ? nombre : valeurParDefaut;

    /// <summary>Change la valeur d'un réglage existant. Renvoie true si la clé existe.</summary>
    public static bool Modifier(string cle, string valeur) =>
        Database.Executer(
            "UPDATE parametre SET valeur = @valeur WHERE cle = @cle",
            Database.P("@valeur", valeur.Trim()),
            Database.P("@cle", cle)) > 0;

    /// <summary>Transforme une ligne de la table en objet Parametre.</summary>
    private static Parametre VersParametre(NpgsqlDataReader r) => new Parametre
    {
        Cle         = r.Texte("cle"),
        Valeur      = r.Texte("valeur"),
        Description = r.TexteOuNull("description")
    };
}
