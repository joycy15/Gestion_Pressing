// =====================================================================
//  EmailEnvoye.cs  —  Une ligne du journal des emails
// ---------------------------------------------------------------------
//  Correspond à la table « email_envoye » de PostgreSQL.
//  Chaque tentative d'envoi d'email à un client est NOTÉE ici, qu'elle
//  réussisse ou non (règle RG16 : tout email est tracé).
//
//  Deux types d'emails (enum TypeEmail) :
//    - Pret          : « Votre commande est prête, venez la récupérer »
//    - Avertissement : « Commande non récupérée depuis 1 mois : elle sera
//                        vendue si vous ne passez pas sous 7 jours »
//
//  Ce que fait la base automatiquement :
//    - si Resultat = Echec, elle crée une notification « EmailEchec »
//      pour l'administrateur (il pourra renvoyer l'email à la main) ;
//    - elle empêche d'envoyer DEUX FOIS le même email automatique
//      réussi pour une commande (index unique uq_email_automatique).
//      Un renvoi manuel (Manuel = true) reste toujours possible.
//
//  Colonnes en base (snake_case)   ->   propriétés en C# (PascalCase)
//      id_email                    ->   IdEmail
//      id_commande                 ->   IdCommande
//      type                        ->   Type
//      destinataire                ->   Destinataire
//      date_envoi                  ->   DateEnvoi
//      resultat                    ->   Resultat
//      erreur                      ->   Erreur      (vide si envoyé)
//      manuel                      ->   Manuel
//
//  Utilisé par : le service d'envoi d'emails (BLL) qui écrit une ligne
//  après chaque tentative, et l'écran « Journal des emails » de
//  l'administrateur.
// =====================================================================

namespace Pressing.Models;

/// <summary>Une tentative d'envoi d'email à un client (table email_envoye).</summary>
public class EmailEnvoye
{
    // ------------------------- Colonnes de la table -------------------------

    /// <summary>Numéro unique de la ligne du journal.</summary>
    public int IdEmail { get; set; }

    /// <summary>La commande concernée par l'email.</summary>
    public int IdCommande { get; set; }

    /// <summary>Quel email : « commande prête » ou « avertissement de mise en vente ».</summary>
    public TypeEmail Type { get; set; } = TypeEmail.Pret;

    /// <summary>Adresse à laquelle l'email a été envoyé (copiée au moment de l'envoi).</summary>
    public string Destinataire { get; set; } = string.Empty;

    /// <summary>Date et heure de la tentative d'envoi (remplie par la base).</summary>
    public DateTime DateEnvoi { get; set; }

    /// <summary>Envoyé ou Échec.</summary>
    public ResultatEmail Resultat { get; set; } = ResultatEmail.Envoye;

    /// <summary>Message d'erreur technique si l'envoi a échoué (vide sinon).</summary>
    public string? Erreur { get; set; }

    /// <summary>Vrai si l'administrateur a renvoyé l'email à la main, faux si envoi automatique.</summary>
    public bool Manuel { get; set; }


    // ------------------------- Affichage -------------------------

    /// <summary>Numéro du ticket de la commande (lu par jointure avec la table commande).</summary>
    public string NumeroTicket { get; set; } = string.Empty;

    /// <summary>Raccourci : vrai si l'envoi a échoué (pour colorer la ligne en rouge, par exemple).</summary>
    public bool EstEnEchec => Resultat == ResultatEmail.Echec;

    /// <summary>Exemple : « PR-2026-000004 - Commande prête -> client@mail.com : Envoyé ».</summary>
    public override string ToString() =>
        $"{NumeroTicket} - {Libelles.De(Type)} -> {Destinataire} : {Libelles.De(Resultat)}";
}
