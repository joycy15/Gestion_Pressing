# PressiGest – Gestion de pressing

Application de bureau Windows qui suit chaque vêtement **du dépôt au retrait ou à la livraison** :
le caissier enregistre la commande avec son service et le choix du client (retrait au pressing
ou livraison à domicile), l'application l'envoie au laveur ou directement au repasseur,
le client reçoit un email quand sa commande est prête, le livreur apporte les commandes
à livrer, et une commande non retirée un mois après est annoncée puis vendue.

Projet de groupe – Keyce Informatique & Intelligence Artificielle, Bachelor 2.
Référence fonctionnelle : **Cahier des charges v2** (dossier `docs/`).

---

## Fonctionnalités

| Module | Ce qu'il fait |
| --- | --- |
| Connexion et utilisateurs | Connexion par identifiant, 5 rôles, menu adapté au rôle |
| Clients | Fiche client (téléphone et email obligatoires), adresse, historique, reste à payer |
| Services et tarifs | 4 services, prix fixe par vêtement **et** par service (grille de tarifs) |
| Dépôt | Client, service, vêtements, **retrait ou livraison**, total automatique, paiement, ticket imprimé |
| Atelier | Files du laveur et du repasseur, express en premier, notification à chaque nouvelle commande |
| Paiements | Seulement notés : espèces, Orange Money, MTN Mobile Money ; au dépôt, au retrait ou au livreur |
| Retrait | Recherche par ticket ou téléphone, paiement du reste, validation |
| Livraison | File du livreur dès le dépôt, notifications, départ, paiement du reste, « Livrée » ou échec |
| Emails | « Commande prête » et « Avertissement de mise en vente », envoyés automatiquement |
| Ventes | Vente des vêtements non retirés après avertissement et délai de grâce |
| Paramètres | Délais, frais de livraison, coordonnées du pressing, compte email |
| Tableau de bord | Commandes par statut, encaissé du jour, alertes |

## Rôles

| Rôle | Ce qu'il fait |
| --- | --- |
| Administrateur (gérant) | Tout, plus : comptes, services, prix, paramètres, annulation, ventes |
| Caissier | Clients, dépôts (choix retrait ou livraison), paiements, retraits, tickets |
| Laveur | Reçoit les commandes à laver, commence et termine le lavage |
| Repasseur | Reçoit les commandes lavées et les « repassage seul », termine le repassage |
| Livreur | Voit dès le dépôt les commandes à livrer, part en livraison, encaisse le reste, valide « Livrée » ou signale un échec |

## Services et circuit d'une commande

| Service | Circuit |
| --- | --- |
| Lavage + repassage | Laveur → Repasseur |
| Lavage express | Laveur → Repasseur, **prioritaire** |
| Nettoyage à sec | Laveur → Repasseur |
| Repassage seul | **Directement** le repasseur |

```
Déposée → En lavage → En repassage → Prête → Retirée                 (client qui vient)
                          ↑             ├─ En livraison → Livrée      (client livré)
      (repassage seul) ───┘             │       └─ échec → retour à Prête
                                        └─ 30 jours sans retrait → Avertie → 7 jours → Vendue
Annulée : possible avant le retrait, le départ en livraison ou la vente (administrateur)
```

- **Au dépôt**, le client choisit **retrait** ou **livraison**. Livraison : adresse du client proposée,
  frais de livraison ajoutés au total (paramètre, 1 000 FCFA par défaut), notification au livreur **dès le dépôt**.
- Passage à **Prête** → email « commande prête » au client (en cas d'échec d'envoi, notification à l'administrateur) ;
  pour une livraison, le livreur est notifié « Prête à livrer ».
- **Livrée** seulement si tout est payé (le reste peut être payé au livreur).
- **30 jours** après « Prête » sans retrait → email d'avertissement, statut **Avertie**.
- **7 jours** de grâce après l'avertissement → l'administrateur peut marquer la commande **Vendue**.

---

## Technologies

| Élément | Choix |
| --- | --- |
| Langage, plateforme | C#, **.NET 10** |
| Interface | Windows Forms |
| Base de données | **PostgreSQL 16+**, outil pgAdmin 4 |
| Accès aux données | ADO.NET avec **Npgsql**, requêtes paramétrées |
| Emails | SMTP (ex. Gmail + mot de passe d'application), bibliothèque **MailKit** |
| Mots de passe | PBKDF2-SHA256 avec sel (inclus dans .NET) |
| Outils | Visual Studio 2026, pgAdmin 4, Git / GitHub |

## Structure du dépôt

```
Gestion_Pressing/
├── database/                 Scripts PostgreSQL (création des tables, données de test)
├── docs/                     Cahier des charges, diagrammes UML, maquettes
├── src/
│   ├── Pressing.Models/      Classes des tables + énumérations
│   ├── Pressing.DAL/         Connexion PostgreSQL, requêtes, transactions
│   ├── Pressing.BLL/         Règles de gestion, mots de passe, envoi des emails
│   └── Pressing.UI/          Écrans Windows Forms (projet de démarrage)
├── appsettings.example.json  Modèle de configuration (sans mot de passe)
├── .gitignore
└── README.md
```

**Règle d'architecture :** `UI → BLL → DAL → base`. Aucune requête SQL dans les formulaires.

---

## Installation (chaque membre)

1. Installer **Visual Studio 2026** (charge « Développement .NET Desktop »), le **SDK .NET 10** et **PostgreSQL 16+** (avec pgAdmin 4).
2. Cloner le dépôt et se placer sur la branche commune :
   ```bash
   git clone https://github.com/joycy15/Gestion_Pressing.git
   cd Gestion_Pressing
   git switch pressing
   ```
3. Dans **pgAdmin** : créer la base `pressing_db`, puis dans son *Query Tool* exécuter dans l'ordre les scripts du dossier `database/`.
4. Copier `appsettings.example.json` en **`src/Pressing.UI/appsettings.json`**, mettre votre mot de passe PostgreSQL
   (et les identifiants du compte email), puis dans Visual Studio : *Propriétés → Copier dans le répertoire de sortie : Copier si plus récent*.
   **Ce fichier ne va jamais sur GitHub.**
5. Ouvrir la solution, définir **Pressing.UI** comme projet de démarrage, lancer avec **F5**.

### Comptes de test

| Identifiant | Mot de passe | Rôle |
| --- | --- | --- |
| admin | Admin@2026 | Administrateur |
| caissier1 | Caisse@2026 | Caissier |
| laveur1 | Laveur@2026 | Laveur |
| repasseur1 | Repasse@2026 | Repasseur |
| livreur1 | Livre@2026 | Livreur |

À changer avant la démonstration.

---

## Conventions

- **Base de données** : noms en minuscules avec `_` (`id_commande`, `numero_ticket`).
- **C#** : `PascalCase` (`IdCommande`, `NumeroTicket`).
- Statuts et rôles stockés **sans accents** : `Deposee`, `EnLavage`, `EnRepassage`, `Prete`, `Avertie`, `EnLivraison`, `Livree`, `Retiree`, `Vendue`, `Annulee` · `Administrateur`, `Caissier`, `Laveur`, `Repasseur`, `Livreur` · `Retrait`, `Livraison`.
- Montants en **FCFA**, nombres entiers.

## Répartition du travail

| Membre | Partie |
| --- | --- |
| Membre 1 (Joycy) | Base de données, Pressing.Models, Pressing.DAL |
| Membre 2 | Connexion, menu par rôle, utilisateurs, services et tarifs, paramètres, tableau de bord |
| Membre 3 | Clients, nouveau dépôt, paiement au dépôt, ticket |
| Membre 4 | Écrans Lavage, Repassage et Livraisons, liste des commandes, historique, annulation |
| Membre 5 | Retrait, emails (MailKit), notifications, avertissements et ventes |

## Travailler avec Git

- Branche commune : **`pressing`**. On ne travaille jamais directement dessus.
- Chacun crée sa branche à partir de `pressing` :
  ```bash
  git fetch
  git switch -c votreprenom origin/pressing
  ```
- Avant de commencer : `git pull origin pressing`.
- Commits courts et clairs : `git commit -m "Ajout de la recherche client"`.
- Pour partager : `git push`, puis **Pull Request `votreprenom` → `pressing`** sur GitHub.
- Ne jamais envoyer `appsettings.json`, `bin/`, `obj/`, `.vs/`.
