/* =====================================================================
   PressiGest - Script 1 : création de la base (PostgreSQL 14+)
   Cahier des charges v2 : 12 tables, 4 rôles, 4 services, emails, ventes.

   AVANT de lancer ce script :
     1. pgAdmin : clic droit sur Databases > Create > Database > nom : pressing_db
     2. Clic droit sur pressing_db > Query Tool > ouvrir ce fichier > Execute (F5)

   Ce script peut être relancé : il supprime et recrée tout.
   ATTENTION : toutes les données existantes sont effacées.
   ===================================================================== */

/* ---------------------- Suppression de l'existant --------------------- */
DROP VIEW IF EXISTS vue_encaissements, vue_commandes_a_vendre, vue_commandes_a_avertir,
                    vue_file_repassage, vue_file_lavage, vue_tarifs, vue_commande_solde CASCADE;
DROP TABLE IF EXISTS notification, email_envoye, historique_statut, paiement, ligne_commande,
                     commande, tarif, type_vetement, service, client, utilisateur, parametre CASCADE;
DROP FUNCTION IF EXISTS fn_utilisateur_courant() CASCADE;
DROP FUNCTION IF EXISTS fn_recalculer_statut_paiement(INT) CASCADE;
DROP FUNCTION IF EXISTS fn_generer_notifications_a_vendre() CASCADE;
DROP FUNCTION IF EXISTS trg_ligne_commande_recalcul() CASCADE;
DROP FUNCTION IF EXISTS trg_paiement_controle() CASCADE;
DROP FUNCTION IF EXISTS trg_commande_avant() CASCADE;
DROP FUNCTION IF EXISTS trg_commande_apres() CASCADE;
DROP FUNCTION IF EXISTS trg_email_echec() CASCADE;


/* =============================== TABLES =============================== */

-- Paramètres : délais, coordonnées du pressing, serveur email (jamais de mot de passe ici)
CREATE TABLE parametre (
    cle          VARCHAR(50)  PRIMARY KEY,
    valeur       VARCHAR(200) NOT NULL,
    description  VARCHAR(200)
);

CREATE TABLE utilisateur (
    id_utilisateur    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nom               VARCHAR(100) NOT NULL,
    login             VARCHAR(50)  NOT NULL UNIQUE,
    mot_de_passe_hash VARCHAR(100) NOT NULL,          -- PBKDF2-SHA256, Base64
    sel               VARCHAR(50)  NOT NULL,          -- sel aléatoire, Base64
    role              VARCHAR(20)  NOT NULL
        CONSTRAINT ck_utilisateur_role CHECK (role IN ('Administrateur', 'Caissier', 'Laveur', 'Repasseur')),
    actif             BOOLEAN      NOT NULL DEFAULT TRUE,
    date_creation     TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

CREATE TABLE client (
    id_client        INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nom              VARCHAR(100) NOT NULL,
    prenom           VARCHAR(100),
    telephone        VARCHAR(20)  NOT NULL UNIQUE,                          -- RG1
    email            VARCHAR(150) NOT NULL UNIQUE                           -- RG1 : obligatoire
        CONSTRAINT ck_client_email CHECK (email ~* '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    adresse          VARCHAR(200),
    date_inscription TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

-- Les 4 services : Lavage + repassage, Lavage express, Nettoyage à sec, Repassage seul
CREATE TABLE service (
    id_service   INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nom          VARCHAR(50) NOT NULL UNIQUE,
    circuit      VARCHAR(30) NOT NULL
        CONSTRAINT ck_service_circuit CHECK (circuit IN ('LavagePuisRepassage', 'RepassageSeul')),
    delai_jours  INT         NOT NULL CHECK (delai_jours > 0),    -- délai de retrait prévu
    prioritaire  BOOLEAN     NOT NULL DEFAULT FALSE,              -- express : en tête des files (RG8)
    actif        BOOLEAN     NOT NULL DEFAULT TRUE
);

CREATE TABLE type_vetement (
    id_type_vetement INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    libelle          VARCHAR(50) NOT NULL UNIQUE,
    actif            BOOLEAN     NOT NULL DEFAULT TRUE
);

-- Grille de prix : un prix par couple (vêtement, service) (RG4)
CREATE TABLE tarif (
    id_type_vetement INT NOT NULL REFERENCES type_vetement(id_type_vetement),
    id_service       INT NOT NULL REFERENCES service(id_service),
    prix             INT NOT NULL CHECK (prix > 0),               -- FCFA
    PRIMARY KEY (id_type_vetement, id_service)
);

CREATE TABLE commande (
    id_commande         INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    numero_ticket       VARCHAR(20)  NOT NULL UNIQUE,     -- RG3 : rempli par le déclencheur
    id_client           INT          NOT NULL REFERENCES client(id_client),
    id_service          INT          NOT NULL REFERENCES service(id_service),
    id_utilisateur      INT          NOT NULL REFERENCES utilisateur(id_utilisateur),  -- caissier
    date_depot          TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0),
    date_retrait_prevue DATE         NOT NULL,            -- calculée si absente (délai du service)
    date_prete          TIMESTAMP(0),
    date_avertissement  TIMESTAMP(0),
    date_retrait        TIMESTAMP(0),
    date_vente          TIMESTAMP(0),
    prix_vente          INT CHECK (prix_vente IS NULL OR prix_vente >= 0),
    statut              VARCHAR(20)  NOT NULL DEFAULT 'Deposee'
        CONSTRAINT ck_commande_statut CHECK (statut IN
            ('Deposee', 'EnLavage', 'EnRepassage', 'Prete', 'Avertie', 'Retiree', 'Vendue', 'Annulee')),
    -- Calculés automatiquement : ne jamais les saisir à la main
    montant_total       INT          NOT NULL DEFAULT 0 CHECK (montant_total >= 0),
    statut_paiement     VARCHAR(20)  NOT NULL DEFAULT 'NonPaye'
        CONSTRAINT ck_commande_statut_paiement CHECK (statut_paiement IN ('NonPaye', 'PartiellementPaye', 'Paye'))
);

CREATE TABLE ligne_commande (
    id_ligne         INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande      INT NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    id_type_vetement INT NOT NULL REFERENCES type_vetement(id_type_vetement),
    quantite         INT NOT NULL CHECK (quantite > 0),
    prix_unitaire    INT NOT NULL CHECK (prix_unitaire > 0),   -- copié de la grille au dépôt (RG4)
    observation      VARCHAR(200)
);

CREATE TABLE paiement (
    id_paiement    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande    INT          NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    id_utilisateur INT          NOT NULL REFERENCES utilisateur(id_utilisateur),
    montant        INT          NOT NULL CHECK (montant > 0),
    mode_paiement  VARCHAR(20)  NOT NULL                                                   -- RG10
        CONSTRAINT ck_paiement_mode CHECK (mode_paiement IN ('Especes', 'OrangeMoney', 'MobileMoney')),
    moment         VARCHAR(10)  NOT NULL
        CONSTRAINT ck_paiement_moment CHECK (moment IN ('Depot', 'Retrait')),
    date_paiement  TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

-- RG9 : chaque changement de statut, avec l'employé et la date
CREATE TABLE historique_statut (
    id_historique  INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande    INT          NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    ancien_statut  VARCHAR(20),                                   -- NULL à la création
    nouveau_statut VARCHAR(20)  NOT NULL,
    id_utilisateur INT          REFERENCES utilisateur(id_utilisateur),
    date_changement TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

-- Journal des emails (RG16)
CREATE TABLE email_envoye (
    id_email     INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande  INT          NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    type         VARCHAR(20)  NOT NULL
        CONSTRAINT ck_email_type CHECK (type IN ('Pret', 'Avertissement')),
    destinataire VARCHAR(150) NOT NULL,
    date_envoi   TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0),
    resultat     VARCHAR(10)  NOT NULL
        CONSTRAINT ck_email_resultat CHECK (resultat IN ('Envoye', 'Echec')),
    erreur       VARCHAR(500),
    manuel       BOOLEAN      NOT NULL DEFAULT FALSE              -- renvoi manuel par l'administrateur
);

-- Notifications dans l'application, adressées à un rôle (RG7)
CREATE TABLE notification (
    id_notification INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande     INT          NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    pour_role       VARCHAR(20)  NOT NULL
        CONSTRAINT ck_notification_role CHECK (pour_role IN ('Administrateur', 'Caissier', 'Laveur', 'Repasseur')),
    type            VARCHAR(30)  NOT NULL
        CONSTRAINT ck_notification_type CHECK (type IN ('NouvelleCommande', 'EmailEchec', 'AVendre')),
    date_creation   TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0),
    traitee         BOOLEAN      NOT NULL DEFAULT FALSE,
    date_traitement TIMESTAMP(0),
    CONSTRAINT uq_notification UNIQUE (id_commande, type, pour_role)
);


/* ============================== INDEX ================================= */
CREATE INDEX ix_commande_statut     ON commande(statut);
CREATE INDEX ix_commande_client     ON commande(id_client);
CREATE INDEX ix_commande_date_depot ON commande(date_depot);
CREATE INDEX ix_ligne_commande      ON ligne_commande(id_commande);
CREATE INDEX ix_paiement_commande   ON paiement(id_commande);
CREATE INDEX ix_paiement_date       ON paiement(date_paiement);
CREATE INDEX ix_historique_commande ON historique_statut(id_commande);
CREATE INDEX ix_notification_role   ON notification(pour_role, traitee);
CREATE INDEX ix_client_nom          ON client(nom);
-- RG16 : un email automatique réussi n'est jamais envoyé deux fois pour la même commande
CREATE UNIQUE INDEX uq_email_automatique ON email_envoye(id_commande, type)
    WHERE manuel = FALSE AND resultat = 'Envoye';


/* ========================= FONCTIONS UTILES =========================== */

/* Utilisateur connecté, transmis par l'application dans chaque transaction :
       SELECT set_config('app.id_utilisateur', '3', true);
   Sert à remplir l'historique des statuts (RG9). NULL si non transmis. */
CREATE FUNCTION fn_utilisateur_courant()
RETURNS INT
LANGUAGE sql
STABLE
AS $$
    SELECT NULLIF(current_setting('app.id_utilisateur', true), '')::INT;
$$;

/* RG10 + RG11 : contrôle « payé <= total » et recalcul du statut de paiement */
CREATE FUNCTION fn_recalculer_statut_paiement(p_id_commande INT)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
    v_total INT;
    v_paye  INT;
BEGIN
    SELECT montant_total INTO v_total FROM commande WHERE id_commande = p_id_commande;
    IF NOT FOUND THEN
        RETURN;   -- commande en cours de suppression
    END IF;

    SELECT COALESCE(SUM(montant), 0) INTO v_paye FROM paiement WHERE id_commande = p_id_commande;

    IF v_paye > v_total THEN
        RAISE EXCEPTION 'Le total des paiements (% FCFA) ne peut pas dépasser le montant de la commande (% FCFA).',
            v_paye, v_total USING ERRCODE = 'PR001';
    END IF;

    UPDATE commande
    SET statut_paiement = CASE
            WHEN v_paye = 0        THEN 'NonPaye'
            WHEN v_paye >= v_total THEN 'Paye'
            ELSE 'PartiellementPaye'
        END
    WHERE id_commande = p_id_commande
      AND statut_paiement IS DISTINCT FROM (CASE
            WHEN v_paye = 0        THEN 'NonPaye'
            WHEN v_paye >= v_total THEN 'Paye'
            ELSE 'PartiellementPaye'
        END);
END;
$$;


/* ============================ DÉCLENCHEURS ============================ */

/* Lignes : recalcul du montant total (RG4) puis du statut de paiement */
CREATE FUNCTION trg_ligne_commande_recalcul()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_id INT;
BEGIN
    FOR v_id IN
        SELECT DISTINCT x FROM unnest(ARRAY[
            CASE WHEN TG_OP IN ('INSERT', 'UPDATE') THEN NEW.id_commande END,
            CASE WHEN TG_OP IN ('UPDATE', 'DELETE') THEN OLD.id_commande END
        ]) AS x WHERE x IS NOT NULL
    LOOP
        UPDATE commande
        SET montant_total = COALESCE(
                (SELECT SUM(quantite * prix_unitaire) FROM ligne_commande WHERE id_commande = v_id), 0)
        WHERE id_commande = v_id;

        PERFORM fn_recalculer_statut_paiement(v_id);
    END LOOP;
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_ligne_commande_recalcul
AFTER INSERT OR UPDATE OR DELETE ON ligne_commande
FOR EACH ROW EXECUTE FUNCTION trg_ligne_commande_recalcul();


/* Paiements : contrôle RG10 et recalcul du statut de paiement (RG11) */
CREATE FUNCTION trg_paiement_controle()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        PERFORM fn_recalculer_statut_paiement(NEW.id_commande);
    END IF;
    IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND OLD.id_commande <> NEW.id_commande) THEN
        PERFORM fn_recalculer_statut_paiement(OLD.id_commande);
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_paiement_controle
AFTER INSERT OR UPDATE OR DELETE ON paiement
FOR EACH ROW EXECUTE FUNCTION trg_paiement_controle();


/* Commande AVANT enregistrement :
   numéro de ticket (RG3), circuit selon le service (RG5), règles de statut (RG5, RG12, RG17), dates */
CREATE FUNCTION trg_commande_avant()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_circuit VARCHAR(30);
    v_delai   INT;
    v_ok      BOOLEAN;
BEGIN
    IF TG_OP = 'INSERT' THEN
        SELECT circuit, delai_jours INTO v_circuit, v_delai FROM service WHERE id_service = NEW.id_service;

        -- RG3 : numéro unique construit à partir de l'identifiant
        NEW.numero_ticket := 'PR-' || to_char(NEW.date_depot, 'YYYY') || '-' || lpad(NEW.id_commande::TEXT, 6, '0');

        -- Une commande commence toujours au dépôt...
        IF NEW.statut <> 'Deposee' THEN
            RAISE EXCEPTION 'Une nouvelle commande commence toujours au statut « Déposée ».' USING ERRCODE = 'PR005';
        END IF;
        -- ...sauf le repassage seul, envoyé directement au repasseur (RG5)
        IF v_circuit = 'RepassageSeul' THEN
            NEW.statut := 'EnRepassage';
        END IF;

        -- Date de retrait prévue = date de dépôt + délai du service
        IF NEW.date_retrait_prevue IS NULL THEN
            NEW.date_retrait_prevue := NEW.date_depot::DATE + v_delai;
        END IF;

        RETURN NEW;
    END IF;

    -- ----------------------------- UPDATE -----------------------------
    NEW.numero_ticket := OLD.numero_ticket;   -- le numéro ne change jamais

    IF NEW.id_service <> OLD.id_service AND OLD.statut <> 'Deposee' THEN
        RAISE EXCEPTION 'Le service ne peut plus être changé une fois le traitement commencé.' USING ERRCODE = 'PR006';
    END IF;

    IF NEW.statut <> OLD.statut THEN
        -- RG5 / RG17 : seuls ces passages sont autorisés
        v_ok := (OLD.statut, NEW.statut) IN (
                    ('Deposee', 'EnLavage'), ('EnLavage', 'EnRepassage'), ('EnRepassage', 'Prete'),
                    ('Prete', 'Avertie'), ('Prete', 'Retiree'), ('Avertie', 'Retiree'), ('Avertie', 'Vendue'))
             OR (NEW.statut = 'Annulee' AND OLD.statut IN ('Deposee', 'EnLavage', 'EnRepassage', 'Prete', 'Avertie'));

        IF NOT v_ok THEN
            RAISE EXCEPTION 'Passage du statut « % » à « % » interdit.', OLD.statut, NEW.statut USING ERRCODE = 'PR005';
        END IF;

        -- RG12 : retrait seulement si tout est payé
        IF NEW.statut = 'Retiree' AND NEW.statut_paiement <> 'Paye' THEN
            RAISE EXCEPTION 'Retrait impossible : il reste un montant à payer.' USING ERRCODE = 'PR003';
        END IF;

        -- Dates remplies automatiquement
        IF NEW.statut = 'Prete'   AND NEW.date_prete         IS NULL THEN NEW.date_prete         := LOCALTIMESTAMP(0); END IF;
        IF NEW.statut = 'Avertie' AND NEW.date_avertissement IS NULL THEN NEW.date_avertissement := LOCALTIMESTAMP(0); END IF;
        IF NEW.statut = 'Retiree' AND NEW.date_retrait       IS NULL THEN NEW.date_retrait       := LOCALTIMESTAMP(0); END IF;
        IF NEW.statut = 'Vendue'  AND NEW.date_vente         IS NULL THEN NEW.date_vente         := LOCALTIMESTAMP(0); END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_commande_avant
BEFORE INSERT OR UPDATE ON commande
FOR EACH ROW EXECUTE FUNCTION trg_commande_avant();


/* Commande APRÈS enregistrement : historique (RG9) et notifications par rôle (RG7) */
CREATE FUNCTION trg_commande_apres()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.statut = OLD.statut THEN
        RETURN NULL;
    END IF;

    -- RG9 : historique
    INSERT INTO historique_statut (id_commande, ancien_statut, nouveau_statut, id_utilisateur)
    VALUES (NEW.id_commande,
            CASE WHEN TG_OP = 'UPDATE' THEN OLD.statut END,
            NEW.statut,
            COALESCE(fn_utilisateur_courant(), CASE WHEN TG_OP = 'INSERT' THEN NEW.id_utilisateur END));

    -- La commande quitte une file : la notification « nouvelle commande » est traitée
    UPDATE notification
    SET traitee = TRUE, date_traitement = LOCALTIMESTAMP(0)
    WHERE id_commande = NEW.id_commande AND type = 'NouvelleCommande' AND traitee = FALSE;

    -- RG7 : la commande arrive dans la file du laveur ou du repasseur
    IF NEW.statut = 'Deposee' THEN
        INSERT INTO notification (id_commande, pour_role, type)
        VALUES (NEW.id_commande, 'Laveur', 'NouvelleCommande') ON CONFLICT DO NOTHING;
    ELSIF NEW.statut = 'EnRepassage' THEN
        INSERT INTO notification (id_commande, pour_role, type)
        VALUES (NEW.id_commande, 'Repasseur', 'NouvelleCommande') ON CONFLICT DO NOTHING;
    END IF;

    -- Commande terminée : plus rien à traiter
    IF NEW.statut IN ('Retiree', 'Vendue', 'Annulee') THEN
        UPDATE notification
        SET traitee = TRUE, date_traitement = LOCALTIMESTAMP(0)
        WHERE id_commande = NEW.id_commande AND traitee = FALSE;
    END IF;

    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_commande_apres
AFTER INSERT OR UPDATE OF statut ON commande
FOR EACH ROW EXECUTE FUNCTION trg_commande_apres();


/* Email en échec -> notification pour l'administrateur (il pourra le renvoyer) */
CREATE FUNCTION trg_email_echec()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.resultat = 'Echec' THEN
        INSERT INTO notification (id_commande, pour_role, type)
        VALUES (NEW.id_commande, 'Administrateur', 'EmailEchec')
        ON CONFLICT (id_commande, type, pour_role)
        DO UPDATE SET traitee = FALSE, date_traitement = NULL, date_creation = LOCALTIMESTAMP(0);
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_email_echec
AFTER INSERT ON email_envoye
FOR EACH ROW EXECUTE FUNCTION trg_email_echec();


/* ============================== VENTES ================================ */

/* RG15 : crée les notifications « À vendre » pour l'administrateur
   (commandes averties depuis plus de DelaiGraceJours). À appeler au démarrage :
       SELECT fn_generer_notifications_a_vendre();                          */
CREATE FUNCTION fn_generer_notifications_a_vendre()
RETURNS INT
LANGUAGE plpgsql
AS $$
DECLARE
    v_grace INT;
    v_nb    INT;
BEGIN
    SELECT valeur::INT INTO v_grace FROM parametre WHERE cle = 'DelaiGraceJours';
    v_grace := COALESCE(v_grace, 7);

    INSERT INTO notification (id_commande, pour_role, type)
    SELECT id_commande, 'Administrateur', 'AVendre'
    FROM commande
    WHERE statut = 'Avertie'
      AND date_avertissement <= LOCALTIMESTAMP - make_interval(days => v_grace)
    ON CONFLICT DO NOTHING;

    GET DIAGNOSTICS v_nb = ROW_COUNT;
    RETURN v_nb;
END;
$$;


/* =============================== VUES ================================= */

/* Commande complète : client, service, montant payé, reste à payer */
CREATE VIEW vue_commande_solde AS
SELECT
    c.id_commande,
    c.numero_ticket,
    c.id_client,
    cl.nom || COALESCE(' ' || cl.prenom, '')                AS nom_client,
    cl.telephone,
    cl.email,
    c.id_service,
    s.nom                                                    AS service,
    s.prioritaire,
    c.id_utilisateur,
    c.date_depot,
    c.date_retrait_prevue,
    c.date_prete,
    c.date_avertissement,
    c.date_retrait,
    c.date_vente,
    c.prix_vente,
    c.statut,
    c.statut_paiement,
    c.montant_total,
    COALESCE(p.montant_paye, 0)::INT                         AS montant_paye,
    (c.montant_total - COALESCE(p.montant_paye, 0))::INT     AS reste_a_payer
FROM commande c
JOIN client  cl ON cl.id_client  = c.id_client
JOIN service s  ON s.id_service  = c.id_service
LEFT JOIN (SELECT id_commande, SUM(montant) AS montant_paye
           FROM paiement GROUP BY id_commande) p ON p.id_commande = c.id_commande;

/* Grille de tarifs lisible (écran Paramètres, nouveau dépôt) */
CREATE VIEW vue_tarifs AS
SELECT t.id_type_vetement, tv.libelle AS vetement, t.id_service, s.nom AS service, t.prix
FROM tarif t
JOIN type_vetement tv ON tv.id_type_vetement = t.id_type_vetement
JOIN service s        ON s.id_service        = t.id_service
WHERE tv.actif AND s.actif;

/* File du laveur : à laver et en lavage, express en premier (RG8) */
CREATE VIEW vue_file_lavage AS
SELECT v.*, (SELECT SUM(quantite) FROM ligne_commande l WHERE l.id_commande = v.id_commande)::INT AS nb_articles
FROM vue_commande_solde v
WHERE v.statut IN ('Deposee', 'EnLavage')
ORDER BY v.prioritaire DESC, v.date_depot;

/* File du repasseur : lavées et « repassage seul », express en premier (RG8) */
CREATE VIEW vue_file_repassage AS
SELECT v.*, (SELECT SUM(quantite) FROM ligne_commande l WHERE l.id_commande = v.id_commande)::INT AS nb_articles
FROM vue_commande_solde v
WHERE v.statut = 'EnRepassage'
ORDER BY v.prioritaire DESC, v.date_depot;

/* RG14 : commandes prêtes depuis DelaiAvertissementJours -> l'application envoie l'email puis passe à « Avertie » */
CREATE VIEW vue_commandes_a_avertir AS
SELECT v.*
FROM vue_commande_solde v
WHERE v.statut = 'Prete'
  AND v.date_prete <= LOCALTIMESTAMP - make_interval(days =>
        COALESCE((SELECT valeur::INT FROM parametre WHERE cle = 'DelaiAvertissementJours'), 30));

/* RG15 : commandes averties dont le délai de grâce est dépassé -> l'administrateur peut les vendre */
CREATE VIEW vue_commandes_a_vendre AS
SELECT v.*
FROM vue_commande_solde v
WHERE v.statut = 'Avertie'
  AND v.date_avertissement <= LOCALTIMESTAMP - make_interval(days =>
        COALESCE((SELECT valeur::INT FROM parametre WHERE cle = 'DelaiGraceJours'), 7));

/* Encaissements par jour et par mode, commandes annulées exclues (tableau de bord) */
CREATE VIEW vue_encaissements AS
SELECT
    p.date_paiement::DATE AS jour,
    p.mode_paiement,
    SUM(p.montant)::INT   AS montant,
    COUNT(*)::INT         AS nb_paiements
FROM paiement p
JOIN commande c ON c.id_commande = p.id_commande
WHERE c.statut <> 'Annulee'
GROUP BY p.date_paiement::DATE, p.mode_paiement;


SELECT 'Base pressing_db créée (12 tables). Lancez maintenant 02_donnees_test.sql.' AS message;
