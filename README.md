
# 🧺 Gestion_Pressing

> Application de bureau modulaire développée en équipe sous **.NET 10 / C# Windows Forms** et **SQL Server Express**, dédiée à la gestion complète du cycle d'un pressing (dépôt, suivi de traitement, paiements, retraits, notifications et tableau de bord).

---

## 💡 Vision du Projet

**Gestion_Pressing** remplace le suivi manuel sur cahier et les tickets papier au comptoir d'un pressing mono-agence. L'objectif est d'assurer une traçabilité fiable de chaque article du dépôt jusqu'au retrait, d'éviter toute erreur de facturation en **FCFA**, d'encadrer les encaissements (au dépôt, au retrait ou en deux fois) et d'alerter le gérant en temps réel sur les commandes prêtes ou non retirées après 14 jours.

---

## 🚀 Fonctionnalités Clés

* 🔐 **Authentification & Paramètres (`Membre 2`)** : connexion sécurisée avec mot de passe haché, gestion des comptes (`Gérant` / `Caissier`), contrôle d'accès par rôle et configuration des types de vêtements et tarifs fixes (FCFA).
* 👥 **Clients & Suivi des Commandes (`Membre 3`)** : gestion des fiches clients (unicité par numéro de téléphone), historique par client, liste filtrable des commandes, changement de statut (`Déposée` → `Prête` → `Retirée`) et annulation réservée au gérant.
* 🧾 **Dépôt & Paiement au Dépôt (`Membre 4`)** : création d'une commande multi-lignes avec copie du prix unitaire et observations, calcul automatique du total et de la date de retrait prévue, enregistrement transactionnel SQL, numéro de ticket unique (`PR-2026-000123`) et impression via `PrintDocument`.
* 📦 **Retrait, Notifications & Tableau de Bord (`Membre 5`)** : recherche par numéro de ticket ou téléphone, encaissement du solde restant, verrouillage du retrait tant que le reste à payer n'est pas à `0`, alertes (`Prête` et `Non retirée` > 14 jours) et statistiques journalières par mode de paiement.
* 🗄️ **Base de Données & Socle Commun (`Membre 1`)** : schéma SQL Server à 7 tables avec contraintes d'intégrité, jeu de données initiales, bibliothèque partagée `Pressing.Models` et classe de connexion ADO.NET réutilisable.

---

## 🛠️ Stack Technique

* **Langage & Framework** : C# / .NET 10 (LTS) — Windows Forms
* **Base de données & Accès** : SQL Server Express & ADO.NET (`SqlConnection`, `SqlCommand`, `SqlTransaction`, requêtes paramétrées)
* **Impression** : `System.Drawing.Printing.PrintDocument` (tickets de dépôt)
* **Architecture** : Solution en 3 couches strictes (`Pressing.UI`, `Pressing.BLL`, `Pressing.DAL`) + bibliothèque d'entités partagées (`Pressing.Models`)
* **Gestion de version & Collaboration** : Git & GitHub (branches `feature/mX-...` fusionnées sur `main`)

---

## 👥 L'Équipe & Répartition des Rôles

Membres du projet : **Joycy**, **William**, **Apostel**, **Layelle**, **Travis** *(affectation finale en cours — seul le Membre 4 est confirmé)*

| Membre | Nom | Rôle | Périmètre & Écrans | Règles de gestion clés | Préfixe de branche |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Membre 1** | `[À compléter]` | Base de données et socle commun | Script SQL (7 tables), `Pressing.Models`, classe connexion ADO.NET, `.gitignore`, merges | Contraintes PK/FK/UNIQUE, intégrité schéma | `feature/m1-socle-bdd` |
| **Membre 2** | `[À compléter]` | Connexion, utilisateurs et paramètres | `Connexion`, `Menu principal`, `Paramètres` (comptes & tarifs vêtements) | `RG5`, hachage mot de passe, contrôle des rôles | `feature/m2-auth-params` |
| **Membre 3** | `[À compléter]` | Clients et suivi des commandes | `Clients`, `Commandes` (recherche, filtres, statuts `Déposée` → `Prête`, annulation gérant) | `RG1`, `RG5`, `RG11`, `RG13` | `feature/m3-clients-suivi` |
| **Membre 4** | **William** | Dépôt et paiement au dépôt | `Nouveau dépôt` (lignes, calcul total, situation/mode paiement, ticket unique, impression) | `RG2`, `RG3`, `RG4`, `RG6`, `RG7`, `RG8`, `RG9` | `feature/m4-depot-paiement` |
| **Membre 5** | `[À compléter]` | Retrait, notifications et tableau de bord | `Retrait`, `Notifications`, `Tableau de bord` | `RG10`, `RG11`, `RG12`, agrégations CA du jour | `feature/m5-retrait-dashboard` |

---

## 📁 Structure du Dépôt

```text
Gestion_Pressing/
├── database/                      # Scripts SQL Server (création des 7 tables, contraintes, données de test)
├── docs/                          # Cahier des charges et documentation technique
├── src/
│   ├── Pressing.Models/           # Entités partagées (Utilisateur, Client, TypeVetement, Commande, LigneCommande, Paiement, Notification)
│   ├── Pressing.DAL/              # Accès aux données ADO.NET (requêtes SQL paramétrées, transactions)
│   ├── Pressing.BLL/              # Logique métier, calculs financiers et validation des règles RG1 à RG13
│   └── Pressing.UI/               # Formulaires Windows Forms, contrôles et impression PrintDocument
├── .gitignore                     # Fichiers ignorés (.NET / Visual Studio / bin / obj)
└── README.md                      # Présentation du projet, architecture et répartition de l'équipe
