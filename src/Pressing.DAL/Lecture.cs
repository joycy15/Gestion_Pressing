// =====================================================================
//  Lecture.cs  —  Petites aides pour lire une colonne d'une ligne
// ---------------------------------------------------------------------
//  Quand on lit une ligne avec Npgsql, il faut normalement écrire :
//      int i = lecteur.GetOrdinal("id_client");
//      int id = lecteur.GetInt32(i);
//  et vérifier à chaque fois si la colonne est NULL.
//
//  Ces méthodes font tout ça en une seule fois :
//      int id          = lecteur.Int("id_client");
//      string? prenom  = lecteur.TexteOuNull("prenom");
//      StatutCommande s = lecteur.EnumDe<StatutCommande>("statut");
//
//  Ce sont des « méthodes d'extension » (mot-clé « this » devant le
//  premier paramètre) : on les appelle comme si elles faisaient partie
//  de NpgsqlDataReader.
//
//  Rappel : les noms de colonnes sont ceux de la BASE (snake_case).
// =====================================================================

using Npgsql;

namespace Pressing.DAL;

/// <summary>Méthodes d'extension pour lire facilement les colonnes d'une ligne.</summary>
public static class Lecture
{
    // ------------------------- Nombres -------------------------

    /// <summary>Colonne entière obligatoire (INT).</summary>
    public static int Int(this NpgsqlDataReader lecteur, string colonne) =>
        lecteur.GetInt32(lecteur.GetOrdinal(colonne));

    /// <summary>Colonne entière qui peut être NULL (ex. prix_vente).</summary>
    public static int? IntOuNull(this NpgsqlDataReader lecteur, string colonne)
    {
        int i = lecteur.GetOrdinal(colonne);
        return lecteur.IsDBNull(i) ? null : lecteur.GetInt32(i);
    }


    // ------------------------- Texte -------------------------

    /// <summary>Colonne texte obligatoire. Renvoie "" si jamais elle est NULL.</summary>
    public static string Texte(this NpgsqlDataReader lecteur, string colonne)
    {
        int i = lecteur.GetOrdinal(colonne);
        return lecteur.IsDBNull(i) ? string.Empty : lecteur.GetString(i);
    }

    /// <summary>Colonne texte qui peut être NULL (ex. prenom, adresse).</summary>
    public static string? TexteOuNull(this NpgsqlDataReader lecteur, string colonne)
    {
        int i = lecteur.GetOrdinal(colonne);
        return lecteur.IsDBNull(i) ? null : lecteur.GetString(i);
    }


    // ------------------------- Dates -------------------------

    /// <summary>Colonne date / date-heure obligatoire.</summary>
    public static DateTime Date(this NpgsqlDataReader lecteur, string colonne) =>
        lecteur.GetDateTime(lecteur.GetOrdinal(colonne));

    /// <summary>Colonne date qui peut être NULL (ex. date_retrait).</summary>
    public static DateTime? DateOuNull(this NpgsqlDataReader lecteur, string colonne)
    {
        int i = lecteur.GetOrdinal(colonne);
        return lecteur.IsDBNull(i) ? null : lecteur.GetDateTime(i);
    }


    // ------------------------- Vrai / faux -------------------------

    /// <summary>Colonne BOOLEAN.</summary>
    public static bool Bool(this NpgsqlDataReader lecteur, string colonne) =>
        lecteur.GetBoolean(lecteur.GetOrdinal(colonne));


    // ------------------------- Énumérations -------------------------

    /// <summary>
    /// Colonne texte transformée en enum. Fonctionne parce que le texte en
    /// base est EXACTEMENT le nom de la valeur C# (ex. 'EnLavage').
    /// </summary>
    public static T EnumDe<T>(this NpgsqlDataReader lecteur, string colonne) where T : struct, Enum =>
        Enum.Parse<T>(lecteur.GetString(lecteur.GetOrdinal(colonne)));

    /// <summary>Comme EnumDe, mais renvoie null si la colonne est NULL (ex. ancien_statut).</summary>
    public static T? EnumOuNull<T>(this NpgsqlDataReader lecteur, string colonne) where T : struct, Enum
    {
        int i = lecteur.GetOrdinal(colonne);
        return lecteur.IsDBNull(i) ? null : Enum.Parse<T>(lecteur.GetString(i));
    }
}
