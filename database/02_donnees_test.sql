/* =====================================================================
   Gestion_Pressing - Script 2 : données de test (PostgreSQL)
   À lancer APRÈS 01_creation_base.sql, dans le Query Tool de pressing_db.

   Comptes créés :
     admin      / Admin@2026   (Gérant)
     caissier1  / Caisse@2026  (Caissier)
   Changez ces mots de passe avant la démonstration.
   ===================================================================== */

/* ----------------------------- Paramètres ----------------------------- */
INSERT INTO parametre (cle, valeur, description) VALUES
('DelaiRetraitJours', '3',  'Nombre de jours entre le dépôt et la date de retrait prévue (RG6)'),
('DelaiAlerteJours',  '14', 'Nombre de jours après le dépôt avant la notification « Non retirée » (RG13)'),
('NomPressing',       'Pressing Keyce', 'Nom imprimé sur les tickets');

/* ---------------------------- Utilisateurs ---------------------------- */
/* Hash = PBKDF2-SHA256, 100 000 itérations, 32 octets, sel de 16 octets (Base64) */
INSERT INTO utilisateur (nom, login, mot_de_passe_hash, sel, role) VALUES
('Gérant du pressing', 'admin',
 'HPRMAA3GGlkZcb927UHAGCM29EJE+tJjCqWTW26IvSU=', 'tokHT5m/xhfCo21AigHlgg==', 'Gerant'),
('Caissier 1', 'caissier1',
 'EFQ3Vo4uV9l9dnuLlZ63N9cKkxw0AWPLIu42mESmlu8=', 'l+I8Ep0EuLjoWOX1p5y0Kg==', 'Caissier');

/* ------------------------ Types de vêtements -------------------------- */
INSERT INTO type_vetement (libelle, prix_unitaire) VALUES
('Chemise',  500),
('Pantalon', 1000),
('Robe',     1000),
('Nappe',    1000),
('Drap',     2000);

/* ------------------------------ Clients ------------------------------- */
INSERT INTO client (nom, prenom, telephone, adresse) VALUES
('Ngono',  'Marie',   '677112233', 'Bafoussam, Tamdja'),
('Fotso',  'Paul',    '699445566', 'Bafoussam, Djeleng'),
('Mbarga', 'Alice',   '655778899', 'Yaoundé, Bastos'),
('Tchoua', 'Bernard', '682001122', NULL);

/* ----------------------------- Commandes ------------------------------ */
DO $$
DECLARE
    v_admin    INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'admin');
    v_caissier INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'caissier1');

    v_chemise  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Chemise');
    v_pantalon INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Pantalon');
    v_robe     INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Robe');
    v_nappe    INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Nappe');
    v_drap     INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Drap');

    v_c1 INT := (SELECT id_client FROM client WHERE telephone = '677112233');
    v_c2 INT := (SELECT id_client FROM client WHERE telephone = '699445566');
    v_c3 INT := (SELECT id_client FROM client WHERE telephone = '655778899');
    v_c4 INT := (SELECT id_client FROM client WHERE telephone = '682001122');

    v_cmd INT;
BEGIN
    /* Commande 1 : déposée aujourd'hui, payée en partie (2 000 sur 3 500) */
    INSERT INTO commande (id_client, id_utilisateur, date_retrait_prevue)
    VALUES (v_c1, v_caissier, CURRENT_DATE + 3)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) VALUES
    (v_cmd, v_chemise,  3, 500,  'Une chemise blanche avec tache au col'),
    (v_cmd, v_pantalon, 2, 1000, NULL);
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment)
    VALUES (v_cmd, v_caissier, 2000, 'Especes', 'Depot');

    /* Commande 2 : déposée il y a 4 jours, prête, payée en totalité par Orange Money */
    INSERT INTO commande (id_client, id_utilisateur, date_depot, date_retrait_prevue)
    VALUES (v_c2, v_caissier, LOCALTIMESTAMP(0) - INTERVAL '4 days', CURRENT_DATE - 1)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, v_drap,  1, 2000),
    (v_cmd, v_nappe, 1, 1000);
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caissier, 3000, 'OrangeMoney', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '4 days');
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;   -- crée la notification « Prête »

    /* Commande 3 : déposée il y a 20 jours, prête, rien payé -> alerte « Non retirée » */
    INSERT INTO commande (id_client, id_utilisateur, date_depot, date_retrait_prevue)
    VALUES (v_c3, v_admin, LOCALTIMESTAMP(0) - INTERVAL '20 days', CURRENT_DATE - 17)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) VALUES
    (v_cmd, v_robe, 2, 1000, 'Robe rouge : lavage délicat');
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;

    /* Commande 4 : déjà retirée, payée en deux fois (espèces au dépôt, MTN MoMo au retrait) */
    INSERT INTO commande (id_client, id_utilisateur, date_depot, date_retrait_prevue)
    VALUES (v_c4, v_caissier, LOCALTIMESTAMP(0) - INTERVAL '6 days', CURRENT_DATE - 3)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, v_chemise, 1, 500);
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caissier, 200, 'Especes', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '6 days');
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment)
    VALUES (v_cmd, v_caissier, 300, 'MobileMoney', 'Retrait');
    UPDATE commande SET statut = 'Retiree' WHERE id_commande = v_cmd;
END;
$$;

/* Notifications « Non retirée » (commande 3) */
SELECT fn_generer_notifications_non_retirees() AS nouvelles_notifications;

/* ----------------------------- Vérification ---------------------------- */
SELECT numero_ticket, nom_client, statut, statut_paiement, montant_total, montant_paye, reste_a_payer
FROM vue_commande_solde
ORDER BY id_commande;
