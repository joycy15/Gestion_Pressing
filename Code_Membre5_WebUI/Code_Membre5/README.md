# PressiGest – Module Membre 5 (Retrait, Ventes, Notifications, Emails)

Interface **HTML / CSS / JS** (WebView2) branchée sur la **vraie base PostgreSQL `pressing_db`** via `Pressing.DAL`.
Plus aucune simulation : plus de données en mémoire, plus de faux email.

## 1. Préparer la base (une seule fois)

1. Installer PostgreSQL 16+ et pgAdmin 4.
2. pgAdmin : clic droit sur *Databases* > *Create* > *Database* > nom **`pressing_db`**.
3. *Query Tool* sur `pressing_db` : exécuter dans l'ordre
   `database/01_creation_base.sql` puis `database/02_donnees_test.sql`.

## 2. Configurer la connexion (appsettings.json)

Le fichier **`Code_Membre5_WebUI/Code_Membre5/appsettings.json`** est lu par `Pressing.DAL` (copié à côté de l'exe au build).
Il contient 2 valeurs à remplacer :

```json
{
  "ConnectionStrings": {
    "Pressing": "Host=localhost;Port=5432;Database=pressing_db;Username=postgres;Password=VOTRE_MOT_DE_PASSE_POSTGRES"
  },
  "Email": { "MotDePasse": "VOTRE_MOT_DE_PASSE_D_APPLICATION_EMAIL" }
}
```

- `Password` : le mot de passe de l'utilisateur PostgreSQL (celui choisi à l'installation).
- `Email > MotDePasse` : voir étape 3 (inutile tant que vous n'envoyez pas d'emails ; l'application démarre sans).
- Ce fichier est ignoré par Git (`.gitignore`). Modèle partageable : `appsettings.example.json` à la racine.

## 3. Configurer l'envoi des emails (Gmail)

1. Compte Google : activer la validation en 2 étapes, puis créer un **mot de passe d'application** (16 caractères).
2. Le coller dans `appsettings.json` > `Email` > `MotDePasse`.
3. Dans pgAdmin (Query Tool), indiquer le compte expéditeur et les coordonnées du pressing :

```sql
UPDATE parametre SET valeur = 'votre.compte@gmail.com' WHERE cle = 'EmailExpediteur';
UPDATE parametre SET valeur = 'smtp.gmail.com'         WHERE cle = 'SmtpServeur';   -- déjà ainsi par défaut
UPDATE parametre SET valeur = '587'                    WHERE cle = 'SmtpPort';      -- 465 = SSL direct
UPDATE parametre SET valeur = 'Quartier X, Ville'      WHERE cle = 'AdressePressing';
UPDATE parametre SET valeur = '6xx xx xx xx'           WHERE cle = 'TelephonePressing';
```

Délais (30 jours avant avertissement, 7 jours de grâce) : clés `DelaiAvertissementJours` et `DelaiGraceJours`.
Les emails des clients de test (`@example.com`) sont fictifs : remplacez-en un par votre adresse pour tester.

## 4. Lancer

1. Ouvrir `Gestion_Pressing.slnx` dans Visual Studio 2026 (restaure Npgsql, MailKit, WebView2).
2. Projet de démarrage : **Pressing.Membre5.Demo** — F5.
3. Se connecter avec un compte de la table `utilisateur` :
   `admin / Admin@2026` (tout) ou `caissier1 / Caisse@2026` (Retrait uniquement).
   Les autres rôles (laveur, repasseur, livreur) n'ont pas d'écran dans ce module.
4. Prérequis machine : *WebView2 Runtime* (déjà présent sur Windows 10/11 à jour).

## Comportement réel branché sur la base

| Action | Ce qui se passe en base |
| --- | --- |
| Rechercher | `vue_commande_solde` (ticket, nom ou téléphone) |
| Enregistrer paiement | `INSERT INTO paiement` (la base refuse un total supérieur à la commande) |
| Valider retrait | `statut = 'Retiree'` (refusé par la base si tout n'est pas payé) ; historique écrit par le déclencheur |
| Vendre | `statut = 'Vendue'` + `prix_vente` (après avertissement + délai de grâce) |
| Synchroniser (au démarrage, chaque heure, bouton) | commandes « Prête » depuis 30 jours → `Avertie` + email ; notifications « À vendre » ; email « Commande prête » envoyé une seule fois par commande |
| Notifications | table `notification` du rôle Administrateur ; « Marquer traitée » met `traitee = TRUE` |
| Journal emails | table `email_envoye` ; un échec crée automatiquement une notification pour l'administrateur |

Une commande devient « Prête » grâce à l'écran du repasseur (Membre 4). Tant que cet écran n'est pas branché,
pour tester : dans pgAdmin, `UPDATE commande SET statut = 'EnLavage' WHERE numero_ticket = '…';` puis `'EnRepassage'`, puis `'Prete'`
(les passages doivent suivre l'ordre : la base refuse les sauts), puis cliquer sur **Synchroniser**.

## Dépannage

| Message | Cause / solution |
| --- | --- |
| « Fichier appsettings.json introuvable » | Le fichier n'est pas copié : vérifier qu'il existe dans `Code_Membre5/` puis recompiler |
| « Remplacez le texte entre crochets… » | Mot de passe PostgreSQL non remplacé dans `appsettings.json` |
| « Impossible de joindre la base de données » | PostgreSQL arrêté, mauvais Host/Port |
| « La base pressing_db n'existe pas » | Étape 1 non faite |
| Échec d'envoi email dans le journal | Mot de passe d'application, `EmailExpediteur` ou connexion Internet ; renvoyer depuis le journal |
