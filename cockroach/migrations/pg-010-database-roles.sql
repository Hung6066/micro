-- ============================================================================
-- His.Hope EMR - Per-Service Database Roles & Least-Privilege Grants
-- Version: pg-010
-- Description: Creates dedicated PostgreSQL roles (login users) for each
--              microservice and grants CONNECT / schema / table privileges
--              following least-privilege. The application's connection pool
--              connects using these service-specific roles.
--
--              Passwords are set via environment variables; in production,
--              use Vault or GCP Secret Manager to inject them.
--
-- Idempotent: uses IF NOT EXISTS / DROP ... IF EXISTS patterns.
-- Compatible with: PostgreSQL 16+
-- ============================================================================
-- Usage: psql -U postgres -f pg-010-database-roles.sql
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create per-service login roles
-- NOTE: Passwords are set via ALTER ROLE after creation by the secrets
--       management system (Vault). Here we create the roles without passwords
--       and mark them as INHERIT for privilege propagation.
-- ============================================================================

DO $$
BEGIN
    -- Identity Service — owns identitydb
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_identity') THEN
        CREATE ROLE svc_identity LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_patient') THEN
        CREATE ROLE svc_patient LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_appointment') THEN
        CREATE ROLE svc_appointment LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_clinical') THEN
        CREATE ROLE svc_clinical LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_lab') THEN
        CREATE ROLE svc_lab LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_billing') THEN
        CREATE ROLE svc_billing LOGIN INHERIT;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_pharmacy') THEN
        CREATE ROLE svc_pharmacy LOGIN INHERIT;
    END IF;
END
$$;

-- ============================================================================
-- SECTION 2: Create a readonly role for cross-service reporting
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'svc_readonly') THEN
        CREATE ROLE svc_readonly LOGIN INHERIT;
    END IF;
END
$$;

-- ============================================================================
-- SECTION 3: Grant CONNECT on each database to the owning service
-- ============================================================================

-- Primary ownership
GRANT CONNECT ON DATABASE identitydb TO svc_identity;
GRANT CONNECT ON DATABASE patientdb TO svc_patient;
GRANT CONNECT ON DATABASE appointmentdb TO svc_appointment;
GRANT CONNECT ON DATABASE clinicaldb TO svc_clinical;
GRANT CONNECT ON DATABASE his_hope_lab TO svc_lab;
GRANT CONNECT ON DATABASE his_hope_billing TO svc_billing;
GRANT CONNECT ON DATABASE his_hope_pharmacy TO svc_pharmacy;

-- Cross-service read access
GRANT CONNECT ON DATABASE patientdb TO svc_clinical;
GRANT CONNECT ON DATABASE patientdb TO svc_appointment;
GRANT CONNECT ON DATABASE patientdb TO svc_lab;
GRANT CONNECT ON DATABASE patientdb TO svc_billing;
GRANT CONNECT ON DATABASE patientdb TO svc_pharmacy;
GRANT CONNECT ON DATABASE clinicaldb TO svc_billing;
GRANT CONNECT ON DATABASE identitydb TO svc_patient;
GRANT CONNECT ON DATABASE identitydb TO svc_clinical;
GRANT CONNECT ON DATABASE identitydb TO svc_appointment;

-- Readonly gets CONNECT to all databases for monitoring/reporting
GRANT CONNECT ON DATABASE identitydb TO svc_readonly;
GRANT CONNECT ON DATABASE patientdb TO svc_readonly;
GRANT CONNECT ON DATABASE appointmentdb TO svc_readonly;
GRANT CONNECT ON DATABASE clinicaldb TO svc_readonly;
GRANT CONNECT ON DATABASE his_hope_lab TO svc_readonly;
GRANT CONNECT ON DATABASE his_hope_billing TO svc_readonly;
GRANT CONNECT ON DATABASE his_hope_pharmacy TO svc_readonly;

-- ============================================================================
-- SECTION 4: Create a helper function to grant schema+table permissions
--            This avoids repeating the same GRANT pattern for every service.
-- ============================================================================

CREATE OR REPLACE FUNCTION grant_service_access(
    p_service regrole,
    p_target_db name,
    p_full_access_tables name[] DEFAULT NULL
) RETURNS void AS $$
BEGIN
    -- Must be called while connected to p_target_db
    EXECUTE format('GRANT USAGE ON SCHEMA public TO %s', p_service);
    EXECUTE format('GRANT ALL ON ALL TABLES IN SCHEMA public TO %s', p_service);
    EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO %s', p_service);
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- SECTION 5: Full access grants for owning services
--            Each service gets ALL privileges on its own database.
-- ============================================================================

-- This must be executed while connected to each respective database.
-- We accomplish this using a DO block with dblink, but the simpler approach
-- is documented here: run grant_service_access per database.
--
-- For the initial migration, we grant directly:

-- identitydb → svc_identity full access
\c identitydb
GRANT USAGE ON SCHEMA public TO svc_identity;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_identity;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_identity;

-- patientdb → svc_patient full access
\c patientdb
GRANT USAGE ON SCHEMA public TO svc_patient;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_patient;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_patient;

-- appointmentdb → svc_appointment full access
\c appointmentdb
GRANT USAGE ON SCHEMA public TO svc_appointment;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_appointment;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_appointment;

-- clinicaldb → svc_clinical full access
\c clinicaldb
GRANT USAGE ON SCHEMA public TO svc_clinical;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_clinical;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_clinical;

-- his_hope_lab → svc_lab full access
\c labdb
GRANT USAGE ON SCHEMA public TO svc_lab;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_lab;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_lab;

-- his_hope_billing → svc_billing full access
\c billingdb
GRANT USAGE ON SCHEMA public TO svc_billing;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_billing;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_billing;

-- his_hope_pharmacy → svc_pharmacy full access
\c pharmacydb
GRANT USAGE ON SCHEMA public TO svc_pharmacy;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_pharmacy;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO svc_pharmacy;

-- ============================================================================
-- SECTION 6: Cross-service read grants (column-level, PHI minimization)
-- ============================================================================

\c patientdb

-- ClinicalService → limited patient read
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, BloodType, IsActive)
    ON "Patients" TO svc_clinical;

-- AppointmentService → scheduling context
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, IsActive)
    ON "Patients" TO svc_appointment;

-- LabService → lab context
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, BloodType, IsActive)
    ON "Patients" TO svc_lab;

-- BillingService → billing context
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, Phone, Email, InsuranceId, IsActive)
    ON "Patients" TO svc_billing;

-- PharmacyService → prescription context
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, IsActive)
    ON "Patients" TO svc_pharmacy;

\c clinicaldb

-- BillingService → limited encounter read for invoicing
GRANT SELECT (EncounterId, PatientId, AppointmentId, ProviderId, EncounterDate, EncounterType, Status)
    ON "Encounters" TO svc_billing;

\c identitydb

-- ClinicalService → provider lookup (no passwords/security fields)
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON "AspNetUsers" TO svc_clinical;

-- PatientService → provider reference
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON "AspNetUsers" TO svc_patient;

-- AppointmentService → provider scheduling
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON "AspNetUsers" TO svc_appointment;

-- ============================================================================
-- SECTION 7: Readonly grants for monitoring/reporting
-- ============================================================================

-- Readonly svc_readonly gets SELECT-only on all databases

\c identitydb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c patientdb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c appointmentdb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c clinicaldb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c labdb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c billingdb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

\c pharmacydb
GRANT USAGE ON SCHEMA public TO svc_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO svc_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO svc_readonly;

-- Switch back to postgres
\c postgres

-- ============================================================================
-- SECTION 8: Connection pool configuration reference
-- ============================================================================
-- Each .NET service's connection string must use its service-specific role:
--
--   IdentityService:
--     Host=postgres;Database=identitydb;Username=svc_identity;Password=${from_vault}
--
--   PatientService:
--     Host=postgres;Database=patientdb;Username=svc_patient;Password=${from_vault}
--
--   AppointmentService:
--     Host=postgres;Database=appointmentdb;Username=svc_appointment;Password=${from_vault}
--
--   ClinicalService:
--     Host=postgres;Database=clinicaldb;Username=svc_clinical;Password=${from_vault}
--
--   LabService:
--     Host=postgres;Database=his_hope_lab;Username=svc_lab;Password=${from_vault}
--
--   BillingService:
--     Host=postgres;Database=his_hope_billing;Username=svc_billing;Password=${from_vault}
--
--   PharmacyService:
--     Host=postgres;Database=his_hope_pharmacy;Username=svc_pharmacy;Password=${from_vault}
--
-- Passwords are managed by Vault and injected at deployment time.
-- See vault/policies/*-service-policy.hcl for the Vault access policies.
-- ============================================================================

-- ============================================================================
-- SEED DATA SUMMARY (for run-pg-migrations.ps1 compatibility)
-- ============================================================================
-- This migration creates 8 service roles:
--   svc_identity, svc_patient, svc_appointment, svc_clinical,
--   svc_lab, svc_billing, svc_pharmacy, svc_readonly
--
-- Verification query:
--   SELECT rolname FROM pg_roles WHERE rolname LIKE 'svc_%';
-- ============================================================================
