/* =====================================================================
   PressiGest - Script 2 : données de test (PostgreSQL)
   À lancer APRÈS 01_creation_base.sql, dans le Query Tool de pressing_db.

   Comptes créés (à changer avant la démonstration) :
     admin       / Admin@2026    Administrateur
     caissier1   / Caisse@2026   Caissier
     laveur1     / Laveur@2026   Laveur
     repasseur1  / Repasse@2026  Repasseur

   Les prix sont des PRIX DE TEST : l'administrateur les ajustera.
   Les emails des clients sont fictifs (@example.com).
   ===================================================================== */

/* ----------------------------- Paramètres ----------------------------- */
INSERT INTO parametre (cle, valeur, description) VALUES
('DelaiAvertissementJours', '30', 'Jours après « Prête » avant l''email d''avertissement (RG14)'),
('DelaiGraceJours',         '7',  'Jours après l''avertissement avant la vente (RG15)'),
('NomPressing',             'PressiGest', 'Nom imprimé sur les tickets et dans les emails'),
('AdressePressing',         '[ADRESSE DU PRESSING]', 'Adresse imprimée sur les tickets et dans les emails'),
('TelephonePressing',       '[TÉLÉPHONE]', 'Téléphone imprimé sur les tickets et dans les emails'),
('HorairesPressing',        'Lundi - samedi, 8 h - 19 h', 'Horaires rappelés dans l''email « commande prête »'),
('SmtpServeur',             'smtp.gmail.com', 'Serveur d''envoi des emails'),
('SmtpPort',                '587', 'Port SMTP (587 = STARTTLS)'),
('EmailExpediteur',         'pressigest@example.com', 'Adresse d''envoi ; le mot de passe est dans appsettings.json');

/* ---------------------------- Utilisateurs ---------------------------- */
/* Hash = PBKDF2-SHA256, 100 000 itérations, 32 octets, sel de 16 octets (Base64) */
INSERT INTO utilisateur (nom, login, mot_de_passe_hash, sel, role) VALUES
('Gérant du pressing', 'admin',
 'HPRMAA3GGlkZcb927UHAGCM29EJE+tJjCqWTW26IvSU=', 'tokHT5m/xhfCo21AigHlgg==', 'Administrateur'),
('Caissier 1', 'caissier1',
 'EFQ3Vo4uV9l9dnuLlZ63N9cKkxw0AWPLIu42mESmlu8=', 'l+I8Ep0EuLjoWOX1p5y0Kg==', 'Caissier'),
('Laveur 1', 'laveur1',
 'lT+yDhEz/MmHDkGeUeyt5BPwIkWtaCuv5tGUUueTkfs=', 'CBkF4PJ+wEZpAqmjUUy+UQ==', 'Laveur'),
('Repasseur 1', 'repasseur1',
 'bopNNcwL37qKJ0VQxApdOKadWmQ1/xyUnrrnhcJpFpc=', 'NbOAqQC6ZALeXSj52FD7IQ==', 'Repasseur');

/* ------------------------------ Services ------------------------------ */
INSERT INTO service (nom, circuit, delai_jours, prioritaire) VALUES
('Lavage + repassage', 'LavagePuisRepassage', 3, FALSE),
('Lavage express',     'LavagePuisRepassage', 1, TRUE),
('Nettoyage à sec',    'LavagePuisRepassage', 4, FALSE),
('Repassage seul',     'RepassageSeul',       1, FALSE);

/* ------------------------ Types de vêtements -------------------------- */
INSERT INTO type_vetement (libelle) VALUES
('Chemise'), ('Pantalon'), ('Robe'), ('Nappe'), ('Drap');

/* -------------------------- Grille de tarifs -------------------------- */
/* Lavage + repassage = prix donnés par le pressing ; autres services = prix de test */
INSERT INTO tarif (id_type_vetement, id_service, prix)
SELECT tv.id_type_vetement, s.id_service,
       CASE s.nom
           WHEN 'Lavage + repassage' THEN b.prix
           WHEN 'Lavage express'     THEN b.prix * 3 / 2
           WHEN 'Nettoyage à sec'    THEN b.prix * 2
           WHEN 'Repassage seul'     THEN b.prix / 2
       END
FROM type_vetement tv
JOIN (VALUES ('Chemise', 500), ('Pantalon', 1000), ('Robe', 1000), ('Nappe', 1000), ('Drap', 2000))
     AS b(libelle, prix) ON b.libelle = tv.libelle
CROSS JOIN service s;

/* ------------------------------ Clients ------------------------------- */
INSERT INTO client (nom, prenom, telephone, email, adresse) VALUES
('Ngono',  'Marie',   '677112233', 'marie.ngono@example.com',  'Bafoussam, Tamdja'),
('Fotso',  'Paul',    '699445566', 'paul.fotso@example.com',   'Bafoussam, Djeleng'),
('Mbarga', 'Alice',   '655778899', NULL,                       'Yaoundé, Bastos'),
('Tchoua', 'Bernard', '682001122', 'bernard.tchoua@example.com', NULL),
('Kamga',  'Sandrine','690334455', NULL,                       'Bafoussam, Kamkop');

/* ----------------------------- Commandes ------------------------------ */
/* Chaque étape est faite « au nom » du bon employé (set_config), comme le fera l'application,
   pour remplir l'historique des statuts. */
DO $$
DECLARE
    v_admin INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'admin');
    v_caiss INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'caissier1');
    v_lav   INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'laveur1');
    v_rep   INT := (SELECT id_utilisateur FROM utilisateur WHERE login = 'repasseur1');

    s_std   INT := (SELECT id_service FROM service WHERE nom = 'Lavage + repassage');
    s_exp   INT := (SELECT id_service FROM service WHERE nom = 'Lavage express');
    s_sec   INT := (SELECT id_service FROM service WHERE nom = 'Nettoyage à sec');
    s_rep   INT := (SELECT id_service FROM service WHERE nom = 'Repassage seul');

    t_chem  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Chemise');
    t_pant  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Pantalon');
    t_robe  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Robe');
    t_napp  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Nappe');
    t_drap  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Drap');

    c1 INT := (SELECT id_client FROM client WHERE telephone = '677112233');  -- avec email
    c2 INT := (SELECT id_client FROM client WHERE telephone = '699445566');  -- avec email
    c3 INT := (SELECT id_client FROM client WHERE telephone = '655778899');  -- sans email
    c4 INT := (SELECT id_client FROM client WHERE telephone = '682001122');  -- avec email
    c5 INT := (SELECT id_client FROM client WHERE telephone = '690334455');  -- sans email

    v_cmd INT;
BEGIN
    /* 1. Standard, déposée aujourd'hui, avance de 2 000 -> dans la file du laveur */
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO commande (id_client, id_service, id_utilisateur) VALUES (c1, s_std, v_caiss)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) VALUES
    (v_cmd, t_chem, 3, (SELECT prix FROM tarif WHERE id_type_vetement = t_chem AND id_service = s_std), 'Une chemise blanche, tache au col'),
    (v_cmd, t_pant, 2, (SELECT prix FROM tarif WHERE id_type_vetement = t_pant AND id_service = s_std), NULL);
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment)
    VALUES (v_cmd, v_caiss, 2000, 'Especes', 'Depot');

    /* 2. Express, payée par Orange Money, le laveur a commencé */
    INSERT INTO commande (id_client, id_service, id_utilisateur) VALUES (c2, s_exp, v_caiss)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_robe, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_robe AND id_service = s_exp)),
    (v_cmd, t_pant, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_pant AND id_service = s_exp));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment)
    VALUES (v_cmd, v_caiss, 3000, 'OrangeMoney', 'Depot');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage' WHERE id_commande = v_cmd;

    /* 3. Repassage seul : part directement chez le repasseur */
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO commande (id_client, id_service, id_utilisateur) VALUES (c3, s_rep, v_caiss)
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_chem, 4, (SELECT prix FROM tarif WHERE id_type_vetement = t_chem AND id_service = s_rep));

    /* 4. Standard, déposée il y a 5 jours, prête depuis 2 jours, email envoyé */
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c4, s_std, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '5 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_drap, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_drap AND id_service = s_std)),
    (v_cmd, t_napp, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_napp AND id_service = s_std));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caiss, 1000, 'Especes', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '5 days');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete', date_prete = LOCALTIMESTAMP(0) - INTERVAL '2 days' WHERE id_commande = v_cmd;
    INSERT INTO email_envoye (id_commande, type, destinataire, date_envoi, resultat)
    VALUES (v_cmd, 'Pret', 'bernard.tchoua@example.com', LOCALTIMESTAMP(0) - INTERVAL '2 days', 'Envoye');

    /* 5. Nettoyage à sec, prête depuis 35 jours : l'email d'avertissement doit partir */
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c2, s_sec, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '40 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire, observation) VALUES
    (v_cmd, t_robe, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_robe AND id_service = s_sec), 'Robe de soirée, tissu fragile');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete', date_prete = LOCALTIMESTAMP(0) - INTERVAL '35 days' WHERE id_commande = v_cmd;
    INSERT INTO email_envoye (id_commande, type, destinataire, date_envoi, resultat)
    VALUES (v_cmd, 'Pret', 'paul.fotso@example.com', LOCALTIMESTAMP(0) - INTERVAL '35 days', 'Envoye');

    /* 6. Standard, avertie il y a 10 jours : à vendre (délai de grâce dépassé) */
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c1, s_std, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '50 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_chem, 2, (SELECT prix FROM tarif WHERE id_type_vetement = t_chem AND id_service = s_std));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caiss, 500, 'MobileMoney', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '50 days');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete', date_prete = LOCALTIMESTAMP(0) - INTERVAL '45 days' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', '', true);
    UPDATE commande SET statut = 'Avertie', date_avertissement = LOCALTIMESTAMP(0) - INTERVAL '10 days' WHERE id_commande = v_cmd;
    INSERT INTO email_envoye (id_commande, type, destinataire, date_envoi, resultat) VALUES
    (v_cmd, 'Pret',          'marie.ngono@example.com', LOCALTIMESTAMP(0) - INTERVAL '45 days', 'Envoye'),
    (v_cmd, 'Avertissement', 'marie.ngono@example.com', LOCALTIMESTAMP(0) - INTERVAL '10 days', 'Envoye');

    /* 7. Standard, retirée, payée en deux fois (espèces au dépôt, MTN MoMo au retrait) */
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c5, s_std, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '6 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_chem, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_chem AND id_service = s_std));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caiss, 200, 'Especes', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '6 days');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_caiss::TEXT, true);
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment)
    VALUES (v_cmd, v_caiss, 300, 'MobileMoney', 'Retrait');
    UPDATE commande SET statut = 'Retiree' WHERE id_commande = v_cmd;

    /* 8. Standard, prête, client SANS email -> notification « appeler le client » */
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c3, s_std, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '3 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_pant, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_pant AND id_service = s_std));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caiss, 1000, 'OrangeMoney', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '3 days');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;
END;
$$;

/* Notification « À vendre » pour la commande 6 */
SELECT fn_generer_notifications_a_vendre() AS notifications_a_vendre;

/* ----------------------------- Vérification ---------------------------- */
SELECT numero_ticket, nom_client, service, statut, statut_paiement,
       montant_total, montant_paye, reste_a_payer
FROM vue_commande_solde
ORDER BY id_commande;
