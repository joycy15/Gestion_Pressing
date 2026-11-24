/* =====================================================================
   Gestion_Pressing - Script 1 : création des tables (PostgreSQL 14+)

   AVANT de lancer ce script :
     1. Dans pgAdmin, créer la base : clic droit sur Databases > Create > Database
        Nom : pressing_db   (Encoding : UTF8)
     2. Clic droit sur pressing_db > Query Tool, puis ouvrir ce fichier et l'exécuter (F5).

   Ce script peut être relancé : il supprime et recrée tout.
   ATTENTION : toutes les données existantes sont effacées.
   ===================================================================== */

/* ---------- Suppression des objets existants ---------- */
DROP VIEW IF EXISTS vue_encaissements;
DROP VIEW IF EXISTS vue_commande_solde;
DROP TABLE IF EXISTS notification, paiement, ligne_commande, commande,
                     type_vetement, client, utilisateur, parametre CASCADE;
DROP FUNCTION IF EXISTS fn_generer_notifications_non_retirees() CASCADE;
DROP FUNCTION IF EXISTS fn_recalculer_statut_paiement(INT) CASCADE;
DROP FUNCTION IF EXISTS trg_ligne_commande_recalcul() CASCADE;
DROP FUNCTION IF EXISTS trg_paiement_controle() CASCADE;
DROP FUNCTION IF EXISTS trg_commande_avant() CASCADE;
DROP FUNCTION IF EXISTS trg_commande_apres() CASCADE;


/* =============================== TABLES =============================== */

CREATE TABLE parametre (
    cle          VARCHAR(50)  PRIMARY KEY,
    valeur       VARCHAR(100) NOT NULL,
    description  VARCHAR(200)
);

CREATE TABLE utilisateur (
    id_utilisateur    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nom               VARCHAR(100) NOT NULL,
    login             VARCHAR(50)  NOT NULL UNIQUE,
    mot_de_passe_hash VARCHAR(100) NOT NULL,   -- PBKDF2-SHA256 encodé en Base64
    sel               VARCHAR(50)  NOT NULL,   -- sel aléatoire encodé en Base64
    role              VARCHAR(20)  NOT NULL
        CONSTRAINT ck_utilisateur_role CHECK (role IN ('Gerant', 'Caissier')),
    actif             BOOLEAN      NOT NULL DEFAULT TRUE,
    date_creation     TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

CREATE TABLE client (
    id_client        INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nom              VARCHAR(100) NOT NULL,
    prenom           VARCHAR(100),
    telephone        VARCHAR(20)  NOT NULL UNIQUE,                      -- RG1
    adresse          VARCHAR(200),
    date_inscription TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

CREATE TABLE type_vetement (
    id_type_vetement INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    libelle          VARCHAR(50) NOT NULL UNIQUE,
    prix_unitaire    INT         NOT NULL CHECK (prix_unitaire > 0),    -- FCFA
    actif            BOOLEAN     NOT NULL DEFAULT TRUE
);

CREATE TABLE commande (
    id_commande         INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    numero_ticket       VARCHAR(20)  NOT NULL UNIQUE,   -- RG3 : rempli par le déclencheur (PR-2026-000123)
    id_client           INT          NOT NULL REFERENCES client(id_client),
    id_utilisateur      INT          NOT NULL REFERENCES utilisateur(id_utilisateur),
    date_depot          TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0),
    date_retrait_prevue DATE         NOT NULL,
    date_retrait_reelle TIMESTAMP(0),
    statut              VARCHAR(20)  NOT NULL DEFAULT 'Deposee'
        CONSTRAINT ck_commande_statut CHECK (statut IN ('Deposee', 'Prete', 'Retiree', 'Annulee')),
    -- Calculés automatiquement par les déclencheurs : ne jamais les saisir à la main
    montant_total       INT          NOT NULL DEFAULT 0 CHECK (montant_total >= 0),
    statut_paiement     VARCHAR(20)  NOT NULL DEFAULT 'NonPaye'
        CONSTRAINT ck_commande_statut_paiement CHECK (statut_paiement IN ('NonPaye', 'PartiellementPaye', 'Paye'))
);

CREATE TABLE ligne_commande (
    id_ligne         INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande      INT NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    id_type_vetement INT NOT NULL REFERENCES type_vetement(id_type_vetement),
    quantite         INT NOT NULL CHECK (quantite > 0),
    prix_unitaire    INT NOT NULL CHECK (prix_unitaire > 0),   -- copié au moment du dépôt
    observation      VARCHAR(200)
);

CREATE TABLE paiement (
    id_paiement    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande    INT          NOT NULL REFERENCES commande(id_commande),
    id_utilisateur INT          NOT NULL REFERENCES utilisateur(id_utilisateur),
    montant        INT          NOT NULL CHECK (montant > 0),
    mode_paiement  VARCHAR(20)  NOT NULL                                               -- RG8
        CONSTRAINT ck_paiement_mode CHECK (mode_paiement IN ('Especes', 'OrangeMoney', 'MobileMoney')),
    moment         VARCHAR(10)  NOT NULL
        CONSTRAINT ck_paiement_moment CHECK (moment IN ('Depot', 'Retrait')),
    date_paiement  TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0)
);

CREATE TABLE notification (
    id_notification INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_commande     INT          NOT NULL REFERENCES commande(id_commande) ON DELETE CASCADE,
    type            VARCHAR(20)  NOT NULL
        CONSTRAINT ck_notification_type CHECK (type IN ('Prete', 'NonRetiree')),
    date_creation   TIMESTAMP(0) NOT NULL DEFAULT LOCALTIMESTAMP(0),
    traitee         BOOLEAN      NOT NULL DEFAULT FALSE,
    date_traitement TIMESTAMP(0),
    CONSTRAINT uq_notification_commande_type UNIQUE (id_commande, type)               -- RG13
);


/* ============================== INDEX ================================= */
CREATE INDEX ix_commande_statut        ON commande(statut);
CREATE INDEX ix_commande_client        ON commande(id_client);
CREATE INDEX ix_commande_date_depot    ON commande(date_depot);
CREATE INDEX ix_ligne_commande_commande ON ligne_commande(id_commande);
CREATE INDEX ix_paiement_commande      ON paiement(id_commande);
CREATE INDEX ix_paiement_date          ON paiement(date_paiement);
CREATE INDEX ix_notification_traitee   ON notification(traitee);
CREATE INDEX ix_client_nom             ON client(nom);


/* ======================= FONCTION DE RECALCUL ========================= */

/* RG9 + RG10 : contrôle « payé <= total » et recalcul du statut de paiement */
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
        RAISE EXCEPTION 'Le total des paiements (% FCFA) ne peut pas dépasser le montant de la commande (% FCFA) (RG9).',
            v_paye, v_total
            USING ERRCODE = 'PR001';
    END IF;

    UPDATE commande
    SET statut_paiement = CASE
            WHEN v_paye = 0        THEN 'NonPaye'
            WHEN v_paye >= v_total THEN 'Paye'
            ELSE 'PartiellementPaye'
        END
    WHERE id_commande = p_id_commande;
END;
$$;


/* ============================ DÉCLENCHEURS ============================ */

/* Lignes : recalcul de montant_total (RG4) puis du statut de paiement */
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


/* Paiements : contrôle RG9 et recalcul du statut de paiement (RG10) */
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
   numéro de ticket (RG3), règles de statut (RG11), date de retrait réelle */
CREATE FUNCTION trg_commande_avant()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        -- RG3 : numéro unique construit à partir de l'identifiant
        NEW.numero_ticket := 'PR-' || to_char(NEW.date_depot, 'YYYY') || '-' || lpad(NEW.id_commande::TEXT, 6, '0');

        IF NEW.statut = 'Retiree' THEN
            RAISE EXCEPTION 'Une nouvelle commande ne peut pas être déjà retirée.' USING ERRCODE = 'PR003';
        END IF;
    ELSE
        NEW.numero_ticket := OLD.numero_ticket;   -- le numéro ne change jamais

        -- Une commande retirée ou annulée ne change plus de statut
        IF OLD.statut IN ('Retiree', 'Annulee') AND NEW.statut <> OLD.statut THEN
            RAISE EXCEPTION 'Une commande retirée ou annulée ne peut plus changer de statut.' USING ERRCODE = 'PR004';
        END IF;

        -- RG11 : « Retirée » seulement si elle était « Prête » et entièrement payée
        IF NEW.statut = 'Retiree' AND OLD.statut <> 'Retiree'
           AND (OLD.statut <> 'Prete' OR NEW.statut_paiement <> 'Paye') THEN
            RAISE EXCEPTION 'Retrait impossible : la commande doit être prête et entièrement payée (RG11).'
                USING ERRCODE = 'PR003';
        END IF;
    END IF;

    IF NEW.statut = 'Retiree' AND NEW.date_retrait_reelle IS NULL THEN
        NEW.date_retrait_reelle := LOCALTIMESTAMP(0);
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_commande_avant
BEFORE INSERT OR UPDATE ON commande
FOR EACH ROW EXECUTE FUNCTION trg_commande_avant();


/* Commande APRÈS enregistrement : notifications (RG12) */
CREATE FUNCTION trg_commande_apres()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    -- RG12 : notification « Prête » pour le gérant (une seule par commande)
    IF NEW.statut = 'Prete' AND (TG_OP = 'INSERT' OR OLD.statut <> 'Prete') THEN
        INSERT INTO notification (id_commande, type)
        VALUES (NEW.id_commande, 'Prete')
        ON CONFLICT (id_commande, type) DO NOTHING;
    END IF;

    -- Commande retirée ou annulée : ses notifications ne sont plus à traiter
    IF NEW.statut IN ('Retiree', 'Annulee') AND (TG_OP = 'INSERT' OR OLD.statut <> NEW.statut) THEN
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


/* ======================= NOTIFICATIONS 14 JOURS ======================= */

/* RG13 : crée les notifications « Non retirée » pour les commandes déposées
   depuis plus de delai_alerte_jours jours. À appeler à l'ouverture de l'application :
       SELECT fn_generer_notifications_non_retirees();
   Renvoie le nombre de nouvelles notifications. Sans risque de doublon. */
CREATE FUNCTION fn_generer_notifications_non_retirees()
RETURNS INT
LANGUAGE plpgsql
AS $$
DECLARE
    v_delai INT;
    v_nb    INT;
BEGIN
    SELECT valeur::INT INTO v_delai FROM parametre WHERE cle = 'DelaiAlerteJours';
    v_delai := COALESCE(v_delai, 14);

    INSERT INTO notification (id_commande, type)
    SELECT id_commande, 'NonRetiree'
    FROM commande
    WHERE statut IN ('Deposee', 'Prete')
      AND date_depot <= LOCALTIMESTAMP - make_interval(days => v_delai)
    ON CONFLICT (id_commande, type) DO NOTHING;

    GET DIAGNOSTICS v_nb = ROW_COUNT;
    RETURN v_nb;
END;
$$;


/* =============================== VUES ================================= */

/* Commande avec client, montant payé et reste à payer (écrans Commandes, Retrait) */
CREATE VIEW vue_commande_solde AS
SELECT
    c.id_commande,
    c.numero_ticket,
    c.id_client,
    cl.nom || COALESCE(' ' || cl.prenom, '')           AS nom_client,
    cl.telephone,
    c.id_utilisateur,
    c.date_depot,
    c.date_retrait_prevue,
    c.date_retrait_reelle,
    c.statut,
    c.statut_paiement,
    c.montant_total,
    COALESCE(p.montant_paye, 0)::INT                   AS montant_paye,
    (c.montant_total - COALESCE(p.montant_paye, 0))::INT AS reste_a_payer
FROM commande c
JOIN client cl ON cl.id_client = c.id_client
LEFT JOIN (SELECT id_commande, SUM(montant) AS montant_paye
           FROM paiement GROUP BY id_commande) p ON p.id_commande = c.id_commande;

/* Encaissements par jour et par mode, commandes annulées exclues (tableau de bord) */
CREATE VIEW vue_encaissements AS
SELECT
    p.date_paiement::DATE  AS jour,
    p.mode_paiement,
    SUM(p.montant)::INT    AS montant,
    COUNT(*)::INT          AS nb_paiements
FROM paiement p
JOIN commande c ON c.id_commande = p.id_commande
WHERE c.statut <> 'Annulee'
GROUP BY p.date_paiement::DATE, p.mode_paiement;


SELECT 'Base pressing_db créée (8 tables). Lancez maintenant 02_donnees_test.sql.' AS message;
