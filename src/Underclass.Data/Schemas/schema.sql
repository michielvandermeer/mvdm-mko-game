-- Migration version: 202607260001 (InitialTownSchema) [Underclass.Data]
--
-- The persisted shape of a Town. One row in underclass.towns per player; the slice has exactly one
-- hardcoded player, so that row is created on first read and never deleted.
--
-- Areas, Material definitions and Gear base types are absent on purpose: they are content, and content
-- lives in Underclass.Domain's Catalogue. What is stored here is only what a player has done.

CREATE SCHEMA IF NOT EXISTS underclass;

CREATE TABLE underclass.towns (
   town_id uuid NOT NULL PRIMARY KEY,
   gold bigint NOT NULL,
   artisan_count integer NOT NULL,
   explorer_cap integer NOT NULL,
   -- How far continuous accrual (Artisan output and wages) has been settled. Advanced in whole intervals.
   last_settled_at timestamptz NOT NULL
);

CREATE TABLE underclass.gear (
   gear_id uuid NOT NULL PRIMARY KEY,
   town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE,
   base_type text NOT NULL,
   material text NOT NULL,
   power integer NOT NULL
);

CREATE INDEX ix_gear_town ON underclass.gear (town_id);

CREATE TABLE underclass.explorers (
   explorer_id uuid NOT NULL PRIMARY KEY,
   town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE,
   name text NOT NULL,
   skill integer NOT NULL,
   -- Idle, OnExpedition or Dead. A dead Explorer's row stays: the loss is acknowledged, not erased.
   status text NOT NULL,
   equipped_gear_id uuid NULL REFERENCES underclass.gear (gear_id) ON DELETE SET NULL
);

CREATE INDEX ix_explorers_town ON underclass.explorers (town_id);

CREATE TABLE underclass.materials (
   town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE,
   material text NOT NULL,
   amount integer NOT NULL,
   PRIMARY KEY (town_id, material)
);

CREATE TABLE underclass.expeditions (
   expedition_id uuid NOT NULL PRIMARY KEY,
   town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE,
   explorer_id uuid NOT NULL REFERENCES underclass.explorers (explorer_id) ON DELETE CASCADE,
   -- The Gear that went along, recorded here so that it can be destroyed with the Explorer. Deliberately
   -- unconstrained: the row it points at is deleted at the same moment this one is.
   gear_id uuid NOT NULL,
   area text NOT NULL,
   depth integer NOT NULL,
   started_at timestamptz NOT NULL,
   completes_at timestamptz NOT NULL,
   -- Fixed at dispatch, so the estimate the player confirmed is the number the server rolls against.
   survival_chance double precision NOT NULL
);

CREATE INDEX ix_expeditions_due ON underclass.expeditions (town_id, completes_at);

CREATE TABLE underclass.expedition_reports (
   report_id uuid NOT NULL PRIMARY KEY,
   town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE,
   expedition_id uuid NOT NULL,
   explorer_id uuid NOT NULL,
   -- Copied rather than joined: a report has to read on its own long after the Expedition row is gone.
   explorer_name text NOT NULL,
   area text NOT NULL,
   depth integer NOT NULL,
   -- When the Expedition came due, not when anyone happened to look.
   resolved_at timestamptz NOT NULL,
   survived boolean NOT NULL,
   -- What the Explorer was carrying: gained if they returned, forfeited if they did not.
   gold bigint NOT NULL,
   materials jsonb NOT NULL,
   -- The Gear destroyed with them, embedded because its row no longer exists.
   lost_gear jsonb NULL
);

CREATE INDEX ix_reports_town ON underclass.expedition_reports (town_id, resolved_at DESC);
