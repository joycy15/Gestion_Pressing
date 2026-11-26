// =====================================================================
//  ClientRepository.cs  —  Requêtes sur la table client
// ---------------------------------------------------------------------
//  Le téléphone et l'email sont OBLIGATOIRES et UNIQUES (RG1) : si on
//  essaie d'enregistrer un doublon, la base refuse (erreur 23505) et
//  ErreurBase.Message(ex) affiche « Ce numéro est déjà utilisé... ».
//
//  Utilisé par : écran Clients (caissier, administrateur) et écran
//  Nouveau dépôt (rechercher ou créer le client).
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès à la table client.</summary>
public static class ClientRepository
{
    private const string Colonnes =
        "id_client, nom, prenom, telephone, email, adresse, date_inscription";


    // ------------------------- Lecture -------------------------

    /// <summary>Tous les clients, triés par nom puis prénom.</summary>
    public static List<Client> ListerTous() =>
        Database.LireListe($"SELECT {Colonnes} FROM client ORDER BY nom, prenom", VersClient);

    /// <summary>
    /// Recherche par morceau de nom, prénom, téléphone ou email (sans tenir
    /// compte des majuscules : ILIKE). 200 résultats maximum.
    /// Exemple : Rechercher("690") trouve tous les numéros contenant 690.
    /// </summary>
    public static List<Client> Rechercher(string texte) =>
        Database.LireListe(
            $"SELECT {Colonnes} FROM client " +
            "WHERE nom ILIKE @t OR prenom ILIKE @t OR telephone ILIKE @t OR email ILIKE @t " +
            "ORDER BY nom, prenom LIMIT 200",
            VersClient,
            Database.P("@t", "%" + texte.Trim() + "%"));   // % = « n'importe quoi avant / après »

    /// <summary>Un client par son id, ou null.</summary>
    public static Client? ParId(int idClient) =>
        Database.LireUn($"SELECT {Colonnes} FROM client WHERE id_client = @id",
            VersClient, Database.P("@id", idClient));

    /// <summary>Un client par son numéro exact, ou null (vérification rapide au dépôt).</summary>
    public static Client? ParTelephone(string telephone) =>
        Database.LireUn($"SELECT {Colonnes} FROM client WHERE telephone = @tel",
            VersClient, Database.P("@tel", telephone.Trim()));


    // ------------------------- Écriture -------------------------

    /// <summary>Ajoute un client. Renvoie son id.</summary>
    public static int Ajouter(Client c) =>
        Database.InsererEtRenvoyerId(
            "INSERT INTO client (nom, prenom, telephone, email, adresse) " +
            "VALUES (@nom, @prenom, @tel, @email, @adresse) RETURNING id_client",
            Database.P("@nom", c.Nom.Trim()),
            Database.P("@prenom", Vide(c.Prenom)),
            Database.P("@tel", c.Telephone.Trim()),
            Database.P("@email", c.Email.Trim().ToLowerInvariant()),   // emails toujours en minuscules
            Database.P("@adresse", Vide(c.Adresse)));

    /// <summary>Modifie un client. Renvoie true si le client existe.</summary>
    public static bool Modifier(Client c) =>
        Database.Executer(
            "UPDATE client SET nom = @nom, prenom = @prenom, telephone = @tel, email = @email, adresse = @adresse " +
            "WHERE id_client = @id",
            Database.P("@nom", c.Nom.Trim()),
            Database.P("@prenom", Vide(c.Prenom)),
            Database.P("@tel", c.Telephone.Trim()),
            Database.P("@email", c.Email.Trim().ToLowerInvariant()),
            Database.P("@adresse", Vide(c.Adresse)),
            Database.P("@id", c.IdClient)) > 0;


    // ------------------------- Outils -------------------------

    /// <summary>Un texte vide ou fait d'espaces devient NULL en base.</summary>
    private static string? Vide(string? texte) =>
        string.IsNullOrWhiteSpace(texte) ? null : texte.Trim();

    /// <summary>Transforme une ligne de la table en objet Client.</summary>
    private static Client VersClient(NpgsqlDataReader r) => new Client
    {
        IdClient        = r.Int("id_client"),
        Nom             = r.Texte("nom"),
        Prenom          = r.TexteOuNull("prenom"),
        Telephone       = r.Texte("telephone"),
        Email           = r.Texte("email"),
        Adresse         = r.TexteOuNull("adresse"),
        DateInscription = r.Date("date_inscription")
    };
}
