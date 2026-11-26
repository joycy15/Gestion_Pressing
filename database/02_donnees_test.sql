/* =====================================================================
   PressiGest - Script 2 : données de test (PostgreSQL)
   À lancer APRÈS 01_creation_base.sql, dans le Query Tool de pressing_db.

   Comptes créés (à changer avant la démonstration) :
     admin       / Admin@2026    Administrateur
     caissier1   / Caisse@2026   Caissier
     laveur1     / Laveur@2026   Laveur
     repasseur1  / Repasse@2026  Repasseur

   Prix : grille officielle du pressing (lavage + repassage) et règles de calcul
   pour les autres services ; l'administrateur peut tout ajuster.
   Les emails des clients sont fictifs (@example.com) ; l'email est obligatoire.
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

/* ------------------------ Grille des prix du pressing ------------------ */
/* Prix « Lavage + repassage » = grille officielle du pressing.
   Les variantes (petit / moyen / grand…) sans nom sur la grille papier
   ont reçu un nom provisoire : l'administrateur pourra les renommer.      */
DROP TABLE IF EXISTS grille_prix;
CREATE TEMP TABLE grille_prix (libelle VARCHAR(50), prix INT);
INSERT INTO grille_prix (libelle, prix) VALUES
('Chemise / chemisette', 500),
('Pantalon', 500),
('Tee-shirt', 500),
('Robette', 500),
('Veste', 700),
('Costume deux pièces', 1500),
('Costume trois pièces', 1500),
('Tennis (petit)', 1000),
('Tennis (moyen)', 1500),
('Tennis (grand)', 2000),
('Gand (petit)', 500),
('Gand (grand)', 800),
('Sayon (petit)', 500),
('Sayon (grand)', 800),
('Ensemble Gang deux pièces', 1000),
('Ensemble Gang trois pièces (petit)', 1500),
('Ensemble Gang trois pièces (grand)', 2000),
('Gandoura (petit)', 1000),
('Gandoura (moyen)', 1500),
('Gandoura (grand)', 2000),
('Robe soirée (petite)', 1000),
('Robe soirée (moyenne)', 1500),
('Robe soirée (grande)', 2000),
('Robe soirée (très grande)', 3000),
('Serviette (petite)', 500),
('Serviette (moyenne)', 800),
('Serviette (grande)', 1000),
('Ensemble drap (3 pièces)', 1500),
('Ensemble drap (5 pièces)', 2000),
('Drap simple', 1000),
('Couvre-lit (petit)', 2000),
('Couvre-lit (moyen)', 3000),
('Couvre-lit (grand)', 4000),
('Couverture (moyenne)', 3500),
('Couverture (grande)', 5000),
('Sac (petit)', 1000),
('Sac (moyen)', 2000),
('Sac (grand)', 3000),
('Casquette', 500),
('Démembré', 300),
('Chaussette', 200),
('Kabba (petit)', 500),
('Kabba (moyen)', 700),
('Kabba (grand)', 1000),
('Blouson (petit)', 1000),
('Blouson (grand)', 2000),
('Pull-over (petit)', 500),
('Pull-over (grand)', 800),
('Jupe simple (petite)', 500),
('Jupe simple (grande)', 800),
('Jupe plissée', 1000),
('Chaussure en Din (petite)', 1500),
('Chaussure en Din (grande)', 2000),
('Robe mariage et accessoires (simple)', 10000),
('Robe mariage et accessoires (moyenne)', 15000),
('Robe mariage et accessoires (grande)', 20000);

INSERT INTO type_vetement (libelle)
SELECT libelle FROM grille_prix ORDER BY libelle;

/* -------------------------- Grille de tarifs -------------------------- */
/* Règles (exemple chemise : 200 / 500 / 650 / 1 000) :
     Lavage + repassage : prix de la grille
     Repassage seul     : 40 % du prix de la grille
     Lavage express     : prix de la grille + 30 %
     Nettoyage à sec    : 2 fois le prix de la grille
   Arrondi aux 50 FCFA les plus proches, minimum 50 FCFA.                 */
INSERT INTO tarif (id_type_vetement, id_service, prix)
SELECT tv.id_type_vetement, s.id_service,
       GREATEST(50, (ROUND(
           g.prix * CASE s.nom
                        WHEN 'Lavage + repassage' THEN 1.0
                        WHEN 'Repassage seul'     THEN 0.4
                        WHEN 'Lavage express'     THEN 1.3
                        WHEN 'Nettoyage à sec'    THEN 2.0
                    END / 50.0) * 50)::INT)
FROM grille_prix g
JOIN type_vetement tv ON tv.libelle = g.libelle
CROSS JOIN service s;

DROP TABLE grille_prix;

/* ------------------------------ Clients ------------------------------- */
INSERT INTO client (nom, prenom, telephone, email, adresse) VALUES
('Ngono',  'Marie',   '677112233', 'marie.ngono@example.com',  'Bafoussam, Tamdja'),
('Fotso',  'Paul',    '699445566', 'paul.fotso@example.com',   'Bafoussam, Djeleng'),
('Mbarga', 'Alice',   '655778899', 'alice.mbarga@example.com', 'Yaoundé, Bastos'),
('Tchoua', 'Bernard', '682001122', 'bernard.tchoua@example.com', NULL),
('Kamga',  'Sandrine','690334455', 'sandrine.kamga@example.com', 'Bafoussam, Kamkop');

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

    t_chem  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Chemise / chemisette');
    t_pant  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Pantalon');
    t_robe  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Robe soirée (moyenne)');
    t_napp  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Serviette (grande)');
    t_drap  INT := (SELECT id_type_vetement FROM type_vetement WHERE libelle = 'Couvre-lit (moyen)');

    c1 INT := (SELECT id_client FROM client WHERE telephone = '677112233');
    c2 INT := (SELECT id_client FROM client WHERE telephone = '699445566');
    c3 INT := (SELECT id_client FROM client WHERE telephone = '655778899');
    c4 INT := (SELECT id_client FROM client WHERE telephone = '682001122');
    c5 INT := (SELECT id_client FROM client WHERE telephone = '690334455');

    v_cmd INT;
BEGIN
    /* 1. Standard, déposée aujourd'hui, avance de 2 000 sur 2 500 -> dans la file du laveur */
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
    VALUES (v_cmd, v_caiss, 2600, 'OrangeMoney', 'Depot');
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

    /* 8. Standard, prête aujourd'hui, payée : l'email « commande prête » est en ÉCHEC (pas d'Internet)
          -> notification pour l'administrateur, qui pourra le renvoyer */
    INSERT INTO commande (id_client, id_service, id_utilisateur, date_depot)
    VALUES (c3, s_std, v_caiss, LOCALTIMESTAMP(0) - INTERVAL '3 days')
    RETURNING id_commande INTO v_cmd;
    INSERT INTO ligne_commande (id_commande, id_type_vetement, quantite, prix_unitaire) VALUES
    (v_cmd, t_pant, 1, (SELECT prix FROM tarif WHERE id_type_vetement = t_pant AND id_service = s_std));
    INSERT INTO paiement (id_commande, id_utilisateur, montant, mode_paiement, moment, date_paiement)
    VALUES (v_cmd, v_caiss, 500, 'OrangeMoney', 'Depot', LOCALTIMESTAMP(0) - INTERVAL '3 days');
    PERFORM set_config('app.id_utilisateur', v_lav::TEXT, true);
    UPDATE commande SET statut = 'EnLavage'    WHERE id_commande = v_cmd;
    UPDATE commande SET statut = 'EnRepassage' WHERE id_commande = v_cmd;
    PERFORM set_config('app.id_utilisateur', v_rep::TEXT, true);
    UPDATE commande SET statut = 'Prete' WHERE id_commande = v_cmd;
    INSERT INTO email_envoye (id_commande, type, destinataire, resultat, erreur)
    VALUES (v_cmd, 'Pret', 'alice.mbarga@example.com', 'Echec', 'Impossible de joindre le serveur SMTP (pas de connexion Internet)');
END;
$$;

/* Notification « À vendre » pour la commande 6 */
SELECT fn_generer_notifications_a_vendre() AS notifications_a_vendre;

/* ----------------------------- Vérification ---------------------------- */
SELECT numero_ticket, nom_client, service, statut, statut_paiement,
       montant_total, montant_paye, reste_a_payer
FROM vue_commande_solde
ORDER BY id_commande;
