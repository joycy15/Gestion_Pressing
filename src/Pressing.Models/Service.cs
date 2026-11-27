// =====================================================================
//  Service.cs  —  Un service proposé par le pressing
// ---------------------------------------------------------------------
//  Correspond à la table « service » de PostgreSQL.
//  Les 4 services : Lavage + repassage, Lavage express,
//                   Nettoyage à sec, Repassage seul.
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_service                  ->   IdService
//      nom                         ->   Nom
//      circuit                     ->   Circuit       (voir Enums.cs)
//      delai_jours                 ->   DelaiJours
//      prioritaire                 ->   Prioritaire
//      actif                       ->   Actif
//
//  Le service décide :
//    - OÙ part la commande : chez le laveur, ou directement chez le
//      repasseur pour « Repassage seul » (fait automatiquement par la base) ;
//    - la DATE DE RETRAIT prévue (date de dépôt + DelaiJours) ;
//    - l'ORDRE dans les files de l'atelier (Prioritaire = express en premier) ;
//    - le PRIX de chaque vêtement (grille Tarif : vêtement × service).
//
//  Utilisé par : écran Nouveau dépôt, écrans Lavage et Repassage,
//  écran Paramètres (administrateur).
// =====================================================================

namespace Pressing.Models;

/// <summary>Un service du pressing (table service).</summary>
public class Service
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique du service, donné automatiquement par la base.</summary>
    public int IdService { get; set; }

    /// <summary>Nom affiché (ex. « Lavage express »). Unique.</summary>
    public string Nom { get; set; } = string.Empty;

    /// <summary>
    /// Chemin de la commande dans l'atelier :
    ///   LavagePuisRepassage : laveur, puis repasseur ;
    ///   RepassageSeul       : directement le repasseur.
    /// Par défaut : lavage puis repassage.
    /// </summary>
    public Circuit Circuit { get; set; } = Circuit.LavagePuisRepassage;

    /// <summary>
    /// Nombre de jours entre le dépôt et la date de retrait prévue.
    /// Exemple : 1 pour l'express, 3 pour le standard.
    /// </summary>
    public int DelaiJours { get; set; }

    /// <summary>
    /// true pour un service urgent (express) : ses commandes sont affichées
    /// EN PREMIER dans les listes du laveur et du repasseur (RG8).
    /// </summary>
    public bool Prioritaire { get; set; }

    /// <summary>
    /// false = service désactivé : il n'est plus proposé au dépôt,
    /// mais les anciennes commandes qui l'utilisent restent intactes.
    /// </summary>
    public bool Actif { get; set; } = true;


    // --------------------- Raccourci (propriété calculée) ---------------------

    /// <summary>
    /// true si la commande saute le laveur et part directement chez le repasseur.
    /// Exemple d'utilisation à l'écran : afficher « Envoyée au repasseur ».
    /// </summary>
    public bool VaDirectementAuRepasseur => Circuit == Circuit.RepassageSeul;


    // ------------------------------- Affichage -------------------------------

    /// <summary>
    /// Texte affiché dans la liste des services (écran Nouveau dépôt).
    /// Exemple : « Lavage express (1 j) ».
    /// </summary>
    public override string ToString() => $"{Nom} ({DelaiJours} j)";
}
