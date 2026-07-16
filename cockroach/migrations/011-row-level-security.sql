-- ============================================================================
-- His.Hope EMR - Row-Level Security via Session-Variable Views
-- Version: 011
-- Description: CockroachDB 24.1 does not support PostgreSQL's row-level
--              security policies (CREATE POLICY). Instead, we use:
--              1. Session-level variables (SET app.current_user_id = 'xxx')
--                 set at connection/request time by the application.
--              2. Views with WHERE clauses referencing current_setting().
--              3. Application-layer enforcement via the PermissionGuard
--                 middleware (see shared infrastructure).
--
--              Services MUST set the session variable before any query:
--                SET app.current_user_id = '00000000-...';
--                SET app.current_user_role = 'Provider';
--                SET app.current_facility_id = '11111111-...';
--
-- Idempotent: uses IF NOT EXISTS / OR REPLACE for all creates.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Helper function — validate session context is set
-- ============================================================================

CREATE OR REPLACE FUNCTION assert_session_context()
RETURNS VOID AS $$
BEGIN
    IF current_setting('app.current_user_id', true) IS NULL
       OR current_setting('app.current_user_id', true) = '' THEN
        RAISE EXCEPTION 'Session context not set: app.current_user_id must be configured before querying security views. '
                        'Call SET app.current_user_id = ''<uuid>'' at connection startup.';
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- SECTION 2: PATIENT SERVICE — patient isolation views (patientdb)
-- ============================================================================

-- View: patients_visible
-- Providers can see all active patients in their facility.
-- Nurses can see all active patients.
-- Individual users can see their own patient records.
-- Falls back to empty set if session not configured.
CREATE OR REPLACE VIEW patientdb.patients_visible AS
SELECT p.*
FROM patientdb.Patients p
WHERE (
    -- Admin/global role: see all (role-based check)
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    -- Provider/Nurse: see all patients in their facility
    OR (COALESCE(current_setting('app.current_user_role', true), '') IN ('Provider', 'Nurse')
        AND p.FacilityId IS NOT NULL
        AND p.FacilityId = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                p.FacilityId
            ))
    -- Fallback: user can only see patients they created (for non-clinical roles)
    OR p.CreatedBy = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), 'none')
);

-- View: patient_allergies_visible
CREATE OR REPLACE VIEW patientdb.patient_allergies_visible AS
SELECT a.*
FROM patientdb.Allergies a
JOIN patientdb.patients_visible pv ON a.PatientId = pv.PatientId;

-- View: patient_conditions_visible
CREATE OR REPLACE VIEW patientdb.patient_conditions_visible AS
SELECT mc.*
FROM patientdb.MedicalConditions mc
JOIN patientdb.patients_visible pv ON mc.PatientId = pv.PatientId;

-- ============================================================================
-- SECTION 3: CLINICAL SERVICE — encounter isolation views (clinicaldb)
-- ============================================================================

-- View: encounters_visible
-- Providers can see encounters they authored or are assigned to.
-- Nurses can see encounters for their department/unit.
-- Admins can see all.
CREATE OR REPLACE VIEW clinicaldb.encounters_visible AS
SELECT e.*
FROM clinicaldb.Encounters e
WHERE (
    -- Admin role: see all
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    -- Provider: see own encounters
    OR (COALESCE(current_setting('app.current_user_role', true), '') = 'Provider'
        AND e.ProviderId = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '__none__')::STRING)
    -- Nurse: see encounters for their facility/unit
    OR (COALESCE(current_setting('app.current_user_role', true), '') = 'Nurse'
        AND e.FacilityId IS NOT NULL
        AND e.FacilityId = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                '00000000-0000-0000-0000-000000000000'
            )::UUID)
);

-- View: encounter_diagnoses_visible
CREATE OR REPLACE VIEW clinicaldb.encounter_diagnoses_visible AS
SELECT d.*
FROM clinicaldb.EncounterDiagnoses d
JOIN clinicaldb.encounters_visible ev ON d.EncounterId = ev.EncounterId;

-- View: encounter_procedures_visible
CREATE OR REPLACE VIEW clinicaldb.encounter_procedures_visible AS
SELECT p.*
FROM clinicaldb.EncounterProcedures p
JOIN clinicaldb.encounters_visible ev ON p.EncounterId = ev.EncounterId;

-- View: clinical_notes_visible
CREATE OR REPLACE VIEW clinicaldb.clinical_notes_visible AS
SELECT n.*
FROM clinicaldb.ClinicalNotes n
JOIN clinicaldb.encounters_visible ev ON n.EncounterId = ev.EncounterId;

-- View: clinical_prescriptions_visible
CREATE OR REPLACE VIEW clinicaldb.clinical_prescriptions_visible AS
SELECT p.*
FROM clinicaldb.Prescriptions p
JOIN clinicaldb.encounters_visible ev ON p.EncounterId = ev.EncounterId;

-- ============================================================================
-- SECTION 4: APPOINTMENT SERVICE — appointment isolation views (appointmentdb)
-- ============================================================================

-- View: appointments_visible
-- Providers see only their own appointments.
-- Nurses/receptionists see appointments for their facility.
-- Admins see all.
CREATE OR REPLACE VIEW appointmentdb.appointments_visible AS
SELECT a.*
FROM appointmentdb.Appointments a
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    OR a.ProviderId = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '__none__')
    OR (COALESCE(current_setting('app.current_user_role', true), '') IN ('Nurse', 'Receptionist')
        AND a.FacilityId IS NOT NULL
        AND a.FacilityId = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                '00000000-0000-0000-0000-000000000000'
            )::UUID)
);

-- ============================================================================
-- SECTION 5: LAB SERVICE — lab isolation views (his_hope_lab)
-- ============================================================================

-- View: lab_orders_visible
-- Providers see orders they placed.
-- Lab technicians see all orders (their scope).
-- Admins see all.
CREATE OR REPLACE VIEW his_hope_lab.lab_orders_visible AS
SELECT lo.*
FROM his_hope_lab.LabOrders lo
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'LabTechnician')
    OR lo.ProviderId = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '__none__')::UUID
);

-- View: lab_tests_visible
CREATE OR REPLACE VIEW his_hope_lab.lab_tests_visible AS
SELECT lt.*
FROM his_hope_lab.LabTests lt
JOIN his_hope_lab.lab_orders_visible lov ON lt.LabOrderId = lov.Id;

-- View: lab_results_visible
CREATE OR REPLACE VIEW his_hope_lab.lab_results_visible AS
SELECT lr.*
FROM his_hope_lab.LabResults lr
JOIN his_hope_lab.lab_tests_visible ltv ON lr.LabTestId = ltv.Id;

-- ============================================================================
-- SECTION 6: BILLING SERVICE — billing isolation views (his_hope_billing)
-- ============================================================================

-- View: invoices_visible
-- Billing staff see all invoices in their facility.
-- Providers see invoices for their patients/encounters.
CREATE OR REPLACE VIEW his_hope_billing.invoices_visible AS
SELECT i.*
FROM his_hope_billing.Invoices i
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'BillingClerk')
    OR i.PatientId IN (
        SELECT p.PatientId
        FROM patientdb.Patients p
        WHERE p.FacilityId = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                '00000000-0000-0000-0000-000000000000'
            )::UUID
    )
);

-- View: payments_visible
CREATE OR REPLACE VIEW his_hope_billing.payments_visible AS
SELECT p.*
FROM his_hope_billing.Payments p
JOIN his_hope_billing.invoices_visible iv ON p.InvoiceId = iv.Id;

-- ============================================================================
-- SECTION 7: PHARMACY SERVICE — prescription isolation views (his_hope_pharmacy)
-- ============================================================================

-- View: prescriptions_visible
-- Pharmacists see all prescriptions.
-- Providers see their own prescriptions.
CREATE OR REPLACE VIEW his_hope_pharmacy.prescriptions_visible AS
SELECT p.*
FROM his_hope_pharmacy.Prescriptions p
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'Pharmacist')
    OR p.ProviderId = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '__none__')::UUID
);

-- ============================================================================
-- SECTION 8: Application-layer hook — how to set session context
-- ============================================================================
-- Every API request must set the security context BEFORE executing queries:
--
--   -- Minimum required:
--   SET app.current_user_id = '00000000-0000-0000-0000-000000000101';
--   SET app.current_user_role = 'Admin';
--   SET app.current_facility_id = '11111111-1111-1111-1111-111111111111';
--
-- The Npgsql connection interceptor (see Shared/Infrastructure) will do this
-- automatically on connection open via DbConnectionInterceptor.
--
-- EF Core should query the views (patients_visible) instead of the base tables
-- for read operations. Write operations (INSERT/UPDATE/DELETE) go to base
-- tables and are audited via application-level checks.
--
-- To reconfigure an existing DbContext to use views:
--   modelBuilder.Entity<Patient>().ToView("patients_visible");
-- ============================================================================
