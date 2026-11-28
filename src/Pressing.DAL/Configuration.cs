// =====================================================================
//  Configuration.cs  —  Lecture du fichier appsettings.json
// ---------------------------------------------------------------------
//  appsettings.json contient les SECRETS de chaque membre :
//    - la chaîne de connexion PostgreSQL (avec son mot de passe) ;
//    - le mot de passe du compte qui envoie les emails.
//  Ce fichier est ignoré par Git : il ne va JAMAIS sur GitHub.
//  Sur GitHub, il n'y a que le modèle appsettings.example.json.
//
//  Où doit se trouver appsettings.json ?
//    À côté de l'exécutable (dossier bin\Debug\net10.0-windows de
//    Pressing.UI). Dans Visual Studio : clic droit sur le fichier >
//    Propriétés > « Copier dans le répertoire de sortie : Copier si plus
//    récent ».
//
//  Le fichier n'est lu qu'UNE seule fois (au premier besoin), puis gardé
//  en mémoire.
//
//  Utilisation :
//      string connexion = Configuration.ChaineConnexion;
//      string mdpEmail  = Configuration.MotDePasseEmail;   // pour la BLL (emails)
// =====================================================================

using System.Text.Json;   // lecteur JSON intégré à .NET (pas de paquet à installer)

namespace Pressing.DAL;

/// <summary>Lit les secrets de appsettings.json (chaîne de connexion, mot de passe email).</summary>
public static class Configuration
{
    /// <summary>Nom du fichier de configuration cherché à côté de l'exécutable.</summary>
    public const string NomFichier = "appsettings.json";

    // Le document JSON une fois lu (null tant qu'on ne l'a pas encore lu)
    private static JsonDocument? _document;


    // ------------------------- Valeurs disponibles -------------------------

    /// <summary>Chaîne de connexion PostgreSQL (section ConnectionStrings, clé Pressing).</summary>
    public static string ChaineConnexion => LireTexte("ConnectionStrings", "Pressing");

    /// <summary>Mot de passe du compte email expéditeur (section Email, clé MotDePasse).</summary>
    public static string MotDePasseEmail => LireTexte("Email", "MotDePasse");


    // ------------------------- Fonctionnement interne -------------------------

    /// <summary>
    /// Lit une valeur texte « section : clé ».
    /// Lance une erreur claire si la valeur manque, est vide, ou n'a pas
    /// été remplacée (elle contient encore « [MOT_DE_PASSE... »).
    /// </summary>
    private static string LireTexte(string section, string cle)
    {
        JsonElement racine = Document().RootElement;

        // TryGetProperty renvoie false (au lieu de planter) si la clé n'existe pas
        if (!racine.TryGetProperty(section, out JsonElement blocSection) ||
            !blocSection.TryGetProperty(cle, out JsonElement valeur) ||
            valeur.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"La valeur « {section} > {cle} » est absente de {NomFichier}. " +
                "Comparez votre fichier avec appsettings.example.json.");
        }

        string texte = valeur.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(texte))
            throw new InvalidOperationException($"La valeur « {section} > {cle} » est vide dans {NomFichier}.");

        // L'utilisateur a oublié de remplacer le texte du modèle
        if (texte.Contains("[MOT_DE_PASSE"))
            throw new InvalidOperationException(
                $"Remplacez le texte entre crochets de « {section} > {cle} » par votre vrai mot de passe dans {NomFichier}.");

        return texte;
    }

    /// <summary>Lit le fichier la première fois, puis renvoie toujours le même document.</summary>
    private static JsonDocument Document()
    {
        if (_document is not null)
            return _document;

        // AppContext.BaseDirectory = le dossier où se trouve l'exécutable (.exe)
        string chemin = Path.Combine(AppContext.BaseDirectory, NomFichier);

        if (!File.Exists(chemin))
            throw new FileNotFoundException(
                $"Fichier {NomFichier} introuvable à côté de l'application. " +
                "Copiez appsettings.example.json en src/Pressing.UI/appsettings.json, " +
                "mettez vos mots de passe, puis réglez « Copier dans le répertoire de sortie : Copier si plus récent ».",
                chemin);

        // Options : on accepte les commentaires // et les virgules en trop
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        _document = JsonDocument.Parse(File.ReadAllText(chemin), options);
        return _document;
    }
}
