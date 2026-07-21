-- Migration: Create missing CriticalAlert tables for labdb
-- These tables were added via EF Core migrations after EnsureCreated() was already called.
-- Run against the local dev postgres container.

BEGIN;

-- CriticalAlertRules
CREATE TABLE IF NOT EXISTS "CriticalAlertRules" (
    "id" UUID PRIMARY KEY,
    "testcode" VARCHAR(20) NOT NULL,
    "testname" VARCHAR(200) NOT NULL,
    "unit" VARCHAR(50),
    "lowcriticalvalue" DECIMAL,
    "highcriticalvalue" DECIMAL,
    "isactive" BOOLEAN NOT NULL DEFAULT true,
    "createdbyuserid" VARCHAR(100) NOT NULL,
    "createdbydisplayname" VARCHAR(200) NOT NULL,
    "createdat" TIMESTAMP NOT NULL DEFAULT NOW(),
    "updatedat" TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "ix_criticalalertrules_testcode" ON "CriticalAlertRules"("testcode");
CREATE INDEX IF NOT EXISTS "ix_criticalalertrules_testcode_isactive" ON "CriticalAlertRules"("testcode", "isactive");

-- CriticalAlerts
CREATE TABLE IF NOT EXISTS "CriticalAlerts" (
    "id" UUID PRIMARY KEY,
    "laborderid" UUID NOT NULL,
    "labtestid" UUID NOT NULL,
    "labresultid" UUID NOT NULL,
    "ruleid" UUID REFERENCES "CriticalAlertRules"("id"),
    "triggertype" VARCHAR(30) NOT NULL,
    "status" VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    "message" VARCHAR(1000) NOT NULL,
    "resultvalue" VARCHAR(500) NOT NULL,
    "resultunit" VARCHAR(50),
    "thresholdvalue" DECIMAL,
    "createdat" TIMESTAMP NOT NULL DEFAULT NOW(),
    "updatedat" TIMESTAMP NOT NULL DEFAULT NOW(),
    "acknowledgedat" TIMESTAMP,
    "acknowledgedbyuserid" VARCHAR(100),
    "acknowledgedbydisplayname" VARCHAR(200),
    "resolvedat" TIMESTAMP,
    "resolvedbyuserid" VARCHAR(100),
    "resolvedbydisplayname" VARCHAR(200)
);

CREATE UNIQUE INDEX IF NOT EXISTS "ix_criticalalerts_laborderid_labtestid"
    ON "CriticalAlerts"("laborderid", "labtestid")
    WHERE "status" <> 'RESOLVED';

CREATE INDEX IF NOT EXISTS "ix_criticalalerts_status"
    ON "CriticalAlerts"("status");

-- CriticalAlertAuditEntries
CREATE TABLE IF NOT EXISTS "CriticalAlertAuditEntries" (
    "id" UUID PRIMARY KEY,
    "criticalalertid" UUID NOT NULL REFERENCES "CriticalAlerts"("id") ON DELETE CASCADE,
    "action" VARCHAR(50) NOT NULL,
    "actoruserid" VARCHAR(100) NOT NULL,
    "actordisplayname" VARCHAR(200) NOT NULL,
    "notes" VARCHAR(1000),
    "occurredat" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "ix_criticalalertauditentries_criticalalertid"
    ON "CriticalAlertAuditEntries"("criticalalertid");

CREATE INDEX IF NOT EXISTS "ix_criticalalertauditentries_occurredat"
    ON "CriticalAlertAuditEntries"("occurredat");

COMMIT;
