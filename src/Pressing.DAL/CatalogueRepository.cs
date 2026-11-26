// =====================================================================
//  CatalogueRepository.cs  —  Services, types de vêtements et tarifs
// ---------------------------------------------------------------------
//  Le « catalogue » du pressing, géré par l'administrateur :
//    - service        : Lavage + repassage, Lavage express, Nettoyage à
//                       sec, Repassage seul (avec circuit et délai) ;
//    - type_vetement  : Chemise, Pantalon, Costume... ;
//    - tarif          : le prix d'un vêtement POUR un service (RG4).
//
//  On ne supprime ni service ni type de vêtement (ils sont liés aux
//  anciennes commandes) : on les DÉSACTIVE (actif = FALSE).
//  Un tarif, lui, peut être supprimé : les commandes gardent le prix
//  copié au moment du dépôt (colonne prix_unitaire de ligne_commande).
//
//  Utilisé par : écran Paramètres > Services / Vêtements / Tarifs
//  (administrateur), et écran Nouveau dépôt (listes et prix).
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Accès aux tables service, type_vetement et tarif.</summary>
public static class CatalogueRepository
{
    // =====================================================================
    //  SERVICES
    // =====================================================================

    private const string ColonnesService = "id_service, nom, circuit, delai_jours, prioritaire, actif";

    /// <summary>Les services, triés par nom. actifsSeulement = false pour l'écran d'administration.</summary>
    public static List<Service> ListerServices(bool actifsSeulement = true) =>
        Database.LireListe(
            $"SELECT {ColonnesService} FROM service " +
            (actifsSeulement ? "WHERE actif " : "") +        // texte fixe : pas de risque d'injection
            "ORDER BY nom",
            VersService);

    /// <summary>Un service par son id, ou null.</summary>
    public static Service? ServiceParId(int idService) =>
        Database.LireUn($"SELECT {ColonnesService} FROM service WHERE id_service = @id",
            VersService, Database.P("@id", idService));

    /// <summary>Ajoute un service. Renvoie son id.</summary>
    public static int AjouterService(Service s) =>
        Database.InsererEtRenvoyerId(
            "INSERT INTO service (nom, circuit, delai_jours, prioritaire, actif) " +
            "VALUES (@nom, @circuit, @delai, @prio, @actif) RETURNING id_service",
            Database.P("@nom", s.Nom.Trim()),
            Database.P("@circuit", s.Circuit.ToString()),
            Database.P("@delai", s.DelaiJours),
            Database.P("@prio", s.Prioritaire),
            Database.P("@actif", s.Actif));

    /// <summary>Modifie un service. Renvoie true s'il existe.</summary>
    public static bool ModifierService(Service s) =>
        Database.Executer(
            "UPDATE service SET nom = @nom, circuit = @circuit, delai_jours = @delai, " +
            "prioritaire = @prio, actif = @actif WHERE id_service = @id",
            Database.P("@nom", s.Nom.Trim()),
            Database.P("@circuit", s.Circuit.ToString()),
            Database.P("@delai", s.DelaiJours),
            Database.P("@prio", s.Prioritaire),
            Database.P("@actif", s.Actif),
            Database.P("@id", s.IdService)) > 0;

    private static Service VersService(NpgsqlDataReader r) => new Service
    {
        IdService   = r.Int("id_service"),
        Nom         = r.Texte("nom"),
        Circuit     = r.EnumDe<Circuit>("circuit"),
        DelaiJours  = r.Int("delai_jours"),
        Prioritaire = r.Bool("prioritaire"),
        Actif       = r.Bool("actif")
    };


    // =====================================================================
    //  TYPES DE VÊTEMENTS
    // =====================================================================

    /// <summary>Les types de vêtements, triés par libellé.</summary>
    public static List<TypeVetement> ListerTypesVetement(bool actifsSeulement = true) =>
        Database.LireListe(
            "SELECT id_type_vetement, libelle, actif FROM type_vetement " +
            (actifsSeulement ? "WHERE actif " : "") +
            "ORDER BY libelle",
            VersTypeVetement);

    /// <summary>Ajoute un type de vêtement. Renvoie son id.</summary>
    public static int AjouterTypeVetement(string libelle) =>
        Database.InsererEtRenvoyerId(
            "INSERT INTO type_vetement (libelle) VALUES (@libelle) RETURNING id_type_vetement",
            Database.P("@libelle", libelle.Trim()));

    /// <summary>Renomme ou (dés)active un type de vêtement.</summary>
    public static bool ModifierTypeVetement(TypeVetement t) =>
        Database.Executer(
            "UPDATE type_vetement SET libelle = @libelle, actif = @actif WHERE id_type_vetement = @id",
            Database.P("@libelle", t.Libelle.Trim()),
            Database.P("@actif", t.Actif),
            Database.P("@id", t.IdTypeVetement)) > 0;

    private static TypeVetement VersTypeVetement(NpgsqlDataReader r) => new TypeVetement
    {
        IdTypeVetement = r.Int("id_type_vetement"),
        Libelle        = r.Texte("libelle"),
        Actif          = r.Bool("actif")
    };


    // =====================================================================
    //  TARIFS
    // =====================================================================

    // Le tarif avec les noms du vêtement et du service (pour l'affichage)
    private const string SelectTarif =
        "SELECT t.id_type_vetement, t.id_service, t.prix, tv.libelle AS vetement, s.nom AS service " +
        "FROM tarif t " +
        "JOIN type_vetement tv ON tv.id_type_vetement = t.id_type_vetement " +
        "JOIN service s        ON s.id_service        = t.id_service ";

    /// <summary>
    /// Les vêtements proposés pour UN service, avec leur prix (écran Nouveau dépôt).
    /// Seuls les vêtements et services actifs sont renvoyés.
    /// </summary>
    public static List<Tarif> ListerTarifsDuService(int idService) =>
        Database.LireListe(
            SelectTarif + "WHERE t.id_service = @s AND tv.actif AND s.actif ORDER BY tv.libelle",
            VersTarif, Database.P("@s", idService));

    /// <summary>Toute la grille de prix (écran Paramètres > Tarifs).</summary>
    public static List<Tarif> ListerTousLesTarifs() =>
        Database.LireListe(SelectTarif + "ORDER BY tv.libelle, s.nom", VersTarif);

    /// <summary>Prix d'un vêtement pour un service, ou null si ce couple n'a pas de tarif.</summary>
    public static int? Prix(int idTypeVetement, int idService)
    {
        object? prix = Database.Scalaire(
            "SELECT prix FROM tarif WHERE id_type_vetement = @v AND id_service = @s",
            Database.P("@v", idTypeVetement), Database.P("@s", idService));
        return prix is null ? null : Convert.ToInt32(prix);
    }

    /// <summary>
    /// Crée le tarif s'il n'existe pas, ou change son prix s'il existe déjà
    /// (« ON CONFLICT ... DO UPDATE » = « upsert » en PostgreSQL).
    /// </summary>
    public static void EnregistrerTarif(int idTypeVetement, int idService, int prix) =>
        Database.Executer(
            "INSERT INTO tarif (id_type_vetement, id_service, prix) VALUES (@v, @s, @prix) " +
            "ON CONFLICT (id_type_vetement, id_service) DO UPDATE SET prix = EXCLUDED.prix",
            Database.P("@v", idTypeVetement),
            Database.P("@s", idService),
            Database.P("@prix", prix));

    /// <summary>Supprime un tarif (ce vêtement ne sera plus proposé pour ce service).</summary>
    public static bool SupprimerTarif(int idTypeVetement, int idService) =>
        Database.Executer(
            "DELETE FROM tarif WHERE id_type_vetement = @v AND id_service = @s",
            Database.P("@v", idTypeVetement), Database.P("@s", idService)) > 0;

    private static Tarif VersTarif(NpgsqlDataReader r) => new Tarif
    {
        IdTypeVetement = r.Int("id_type_vetement"),
        IdService      = r.Int("id_service"),
        Prix           = r.Int("prix"),
        NomVetement    = r.Texte("vetement"),
        NomService     = r.Texte("service")
    };
}
