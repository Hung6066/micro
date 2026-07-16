-- ============================================================================
-- His.Hope EMR - Per-Service Database Roles & Least-Privilege Grants
-- Version: 010
-- Description: Creates dedicated database users (roles) for each microservice,
--              grants CONNECT/USAGE per database, and sets up cross-service
--              SELECT-only grants on limited columns for inter-service reads.
-- Idempotent: uses IF NOT EXISTS for all creates.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create per-service database users (roles)
-- NOTE: Passwords should be set via environment variables. In production,
--       use CockroachDB PKI or GCP Secret Manager integration.
-- ============================================================================

-- Identity Service — owns identitydb
CREATE USER IF NOT EXISTS svc_identity;
CREATE USER IF NOT EXISTS svc_patient;
CREATE USER IF NOT EXISTS svc_appointment;
CREATE USER IF NOT EXISTS svc_clinical;
CREATE USER IF NOT EXISTS svc_lab;
CREATE USER IF NOT EXISTS svc_billing;
CREATE USER IF NOT EXISTS svc_pharmacy;

-- ============================================================================
-- SECTION 2: Grant CONNECT on each database only to the owning service
-- ============================================================================

GRANT CONNECT ON DATABASE identitydb TO svc_identity;
GRANT CONNECT ON DATABASE patientdb TO svc_patient;
GRANT CONNECT ON DATABASE appointmentdb TO svc_appointment;
GRANT CONNECT ON DATABASE clinicaldb TO svc_clinical;
GRANT CONNECT ON DATABASE his_hope_lab TO svc_lab;
GRANT CONNECT ON DATABASE his_hope_billing TO svc_billing;
GRANT CONNECT ON DATABASE his_hope_pharmacy TO svc_pharmacy;

-- Also grant CONNECT for read-only cross-service access
GRANT CONNECT ON DATABASE patientdb TO svc_clinical;
GRANT CONNECT ON DATABASE patientdb TO svc_appointment;
GRANT CONNECT ON DATABASE patientdb TO svc_lab;
GRANT CONNECT ON DATABASE patientdb TO svc_billing;
GRANT CONNECT ON DATABASE patientdb TO svc_pharmacy;

GRANT CONNECT ON DATABASE clinicaldb TO svc_billing;
GRANT CONNECT ON DATABASE identitydb TO svc_patient;
GRANT CONNECT ON DATABASE identitydb TO svc_clinical;
GRANT CONNECT ON DATABASE identitydb TO svc_appointment;

-- ============================================================================
-- SECTION 3: Grant USAGE on all schemas within each database
-- ============================================================================

-- Full schema access for owning services
GRANT ALL ON SCHEMA public TO svc_identity;
GRANT ALL ON SCHEMA public TO svc_patient;
GRANT ALL ON SCHEMA public TO svc_appointment;
GRANT ALL ON SCHEMA public TO svc_clinical;
GRANT ALL ON SCHEMA public TO svc_lab;
GRANT ALL ON SCHEMA public TO svc_billing;
GRANT ALL ON SCHEMA public TO svc_pharmacy;

-- Read-only schema access for cross-service readers
GRANT USAGE ON SCHEMA public TO svc_clinical;
GRANT USAGE ON SCHEMA public TO svc_appointment;
GRANT USAGE ON SCHEMA public TO svc_lab;
GRANT USAGE ON SCHEMA public TO svc_billing;
GRANT USAGE ON SCHEMA public TO svc_pharmacy;

-- ============================================================================
-- SECTION 4: Full table grants for owning services
-- ============================================================================

GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_identity;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_patient;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_appointment;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_clinical;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_lab;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_billing;
GRANT ALL ON ALL TABLES IN SCHEMA public TO svc_pharmacy;

-- ============================================================================
-- SECTION 5: Cross-service read grants (limited columns for PHI minimization)
-- ============================================================================

-- ClinicalService → patientdb.Patients (read-only, limited columns)
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, BloodType, IsActive)
    ON patientdb.Patients TO svc_clinical;

-- AppointmentService → patientdb.Patients (read-only, scheduling context)
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, IsActive)
    ON patientdb.Patients TO svc_appointment;

-- LabService → patientdb.Patients (read-only, lab context)
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, BloodType, IsActive)
    ON patientdb.Patients TO svc_lab;

-- BillingService → patientdb.Patients (read-only, billing context)
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, Phone, Email, InsuranceId, IsActive)
    ON patientdb.Patients TO svc_billing;

-- PharmacyService → patientdb.Patients (read-only, prescription context)
GRANT SELECT (PatientId, FirstName, LastName, MiddleName, DateOfBirth, Gender, Phone, IsActive)
    ON patientdb.Patients TO svc_pharmacy;

-- BillingService → clinicaldb.Encounters (read-only, billing context)
GRANT SELECT (EncounterId, PatientId, AppointmentId, ProviderId, EncounterDate, EncounterType, Status)
    ON clinicaldb.Encounters TO svc_billing;

-- ClinicalService → identitydb.AspNetUsers (read-only, limited columns for provider lookup)
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON identitydb.AspNetUsers TO svc_clinical;

-- PatientService → identitydb.AspNetUsers (read-only, provider reference)
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON identitydb.AspNetUsers TO svc_patient;

-- AppointmentService → identitydb.AspNetUsers (read-only, provider scheduling)
GRANT SELECT (Id, UserName, Email, FullName, FacilityId, IsActive)
    ON identitydb.AspNetUsers TO svc_appointment;

-- ============================================================================
-- SECTION 6: Revoke default public access (defense in depth)
-- ============================================================================

-- PUBLIC should not have any access beyond CONNECT
-- CockroachDB default PUBLIC has CONNECT, so explicitly revoke table access
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;

-- ============================================================================
-- SECTION 7: Apply zone configs for multi-region (if not already applied)
-- ============================================================================

ALTER DATABASE identitydb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE patientdb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE appointmentdb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE clinicaldb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE his_hope_lab CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE his_hope_billing CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE his_hope_pharmacy CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';

-- ============================================================================
-- SECTION 8: Nota bene — application connection setup
-- ============================================================================
-- Each service must connect to the database using its respective user:
--   IdentityService  → svc_identity @ identitydb
--   PatientService   → svc_patient  @ patientdb
--   AppointmentService → svc_appointment @ appointmentdb
--   ClinicalService  → svc_clinical @ clinicaldb
--   LabService       → svc_lab      @ his_hope_lab
--   BillingService   → svc_billing  @ his_hope_billing
--   PharmacyService  → svc_pharmacy @ his_hope_pharmacy
--
-- Cross-service queries use the granted SELECT privileges; no service should
-- connect directly to another service's database for writes.
--
-- At application startup, set the session user context for row-level security:
--   SET app.current_user_id = '<user-uuid>';
-- This session variable is used by the security views defined in 011.
-- ============================================================================
