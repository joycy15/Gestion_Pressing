// =====================================================================
//  Parametre.cs  —  Un réglage du pressing
// ---------------------------------------------------------------------
//  Correspond à la table « parametre » de PostgreSQL.
//  C'est une table « clé / valeur » : chaque ligne est un réglage que
//  l'administrateur peut modifier SANS toucher au code.
//
//      cle                       valeur              à quoi ça sert
//      DelaiAvertissementJours   30                  jours après « Prête » avant l'email d'avertissement (RG14)
//      DelaiGraceJours           7                   jours après l'avertissement avant la vente (RG15)
//      NomPressing               PressiGest          tickets et emails
//      AdressePressing           ...                 tickets et emails
//      TelephonePressing         ...                 tickets et emails
//      HorairesPressing          Lundi - samedi...   email « commande prête »
//      SmtpServeur               smtp.gmail.com      serveur d'envoi des emails
//      SmtpPort                  587                 port du serveur d'envoi
//      EmailExpediteur           ...@...             adresse qui envoie les emails
//
//  SÉCURITÉ : le MOT DE PASSE du compte email n'est JAMAIS ici, ni dans
//  le code. Il est uniquement dans appsettings.json (fichier ignoré
//  par Git, jamais envoyé sur GitHub).
//
//  Toutes les valeurs sont stockées en TEXTE. Pour un nombre (délai,
//  port), on utilise la méthode EnEntier() qui convertit le texte.
//
//  Les constantes (const) en bas de la classe donnent le nom exact de
//  chaque clé : on écrit Parametre.DelaiGraceJours au lieu de taper
//  "DelaiGraceJours" à la main (pas de faute de frappe possible).
//
//  Utilisé par : écran Paramètres (administrateur), le service d'emails,
//  l'impression du ticket, le calcul des commandes à avertir / à vendre.
// =====================================================================

namespace Pressing.Models;

/// <summary>Un réglage modifiable par l'administrateur (table parametre).</summary>
public class Parametre
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Nom du réglage (clé primaire), ex. « DelaiGraceJours ».</summary>
    public string Cle { get; set; } = string.Empty;

    /// <summary>Valeur du réglage, toujours en texte, ex. « 7 ».</summary>
    public string Valeur { get; set; } = string.Empty;

    /// <summary>Explication affichée à l'administrateur dans l'écran Paramètres.</summary>
    public string? Description { get; set; }


    // ------------------------------ Méthodes ------------------------------

    /// <summary>
    /// Convertit la valeur en nombre entier.
    /// Si la valeur n'est pas un nombre valide (ex. « abc »), on renvoie
    /// la valeur par défaut donnée, au lieu de faire planter l'application.
    /// Exemple : param.EnEntier(30) renvoie 30 si la valeur est vide ou fausse.
    /// </summary>
    public int EnEntier(int valeurParDefaut) =>
        int.TryParse(Valeur, out int nombre) ? nombre : valeurParDefaut;

    /// <summary>Exemple : « DelaiGraceJours = 7 ».</summary>
    public override string ToString() => $"{Cle} = {Valeur}";


    // --------------- Noms des clés (identiques à la base) ---------------
    // « const » = valeur fixe, connue à la compilation.
    // Utilisation : Parametre.NomPressing  ->  "NomPressing"

    public const string DelaiAvertissementJours = "DelaiAvertissementJours";
    public const string DelaiGraceJours = "DelaiGraceJours";
    public const string NomPressing = "NomPressing";
    public const string AdressePressing = "AdressePressing";
    public const string TelephonePressing = "TelephonePressing";
    public const string HorairesPressing = "HorairesPressing";
    public const string SmtpServeur = "SmtpServeur";
    public const string SmtpPort = "SmtpPort";
    public const string EmailExpediteur = "EmailExpediteur";
}
