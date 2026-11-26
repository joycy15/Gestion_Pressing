// =====================================================================
//  HistoriqueRepository.cs  —  Lecture du journal des statuts
// ---------------------------------------------------------------------
//  La table historique_statut est remplie UNIQUEMENT par la base
//  (trigger trg_commande_apres), à chaque changement de statut.
//  Ici on ne fait donc QUE LIRE : aucun INSERT, aucun UPDATE.
//
//  Utilisé par : onglet « Historique » du détail d'une commande.
// =====================================================================

using Npgsql;
using Pressing.Models;

namespace Pressing.DAL;

/// <summary>Lecture de la table historique_statut.</summary>
public static class HistoriqueRepository
{
    /// <summary>Tous les changements de statut d'une commande, dans l'ordre chronologique.</summary>
    public static List<HistoriqueStatut> ListerParCommande(int idCommande) =>
        Database.LireListe(
            "SELECT h.id_historique, h.id_commande, h.ancien_statut, h.nouveau_statut, " +
            "h.id_utilisateur, h.date_changement, u.nom AS nom_utilisateur " +
            "FROM historique_statut h " +
            "LEFT JOIN utilisateur u ON u.id_utilisateur = h.id_utilisateur " +   // LEFT : l'employé peut être vide
            "WHERE h.id_commande = @id " +
            "ORDER BY h.date_changement, h.id_historique",
            VersHistorique, Database.P("@id", idCommande));

    /// <summary>Transforme une ligne en objet HistoriqueStatut.</summary>
    private static HistoriqueStatut VersHistorique(NpgsqlDataReader r) => new HistoriqueStatut
    {
        IdHistorique   = r.Int("id_historique"),
        IdCommande     = r.Int("id_commande"),
        AncienStatut   = r.EnumOuNull<StatutCommande>("ancien_statut"),   // NULL à la création
        NouveauStatut  = r.EnumDe<StatutCommande>("nouveau_statut"),
        IdUtilisateur  = r.IntOuNull("id_utilisateur"),
        DateChangement = r.Date("date_changement"),
        NomUtilisateur = r.Texte("nom_utilisateur")                        // "" si NULL -> « Système »
    };
}
