-- ============================================================================
-- His.Hope EMR - Row-Level Security via Session-Variable Views
-- Version: pg-011
-- Description: PostgreSQL 16 RLS using session variables (current_setting).
--              Views with WHERE clauses filtering by role and facility.
--              Application-layer enforcement via PermissionGuard middleware.
--
--              Services MUST set session variables before querying:
--                SET app.current_user_id = '00000000-...';
--                SET app.current_user_role = 'Provider';
--                SET app.current_facility_id = '11111111-...';
--
-- Idempotent: uses OR REPLACE for views.
-- Compatible with: PostgreSQL 16+
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

\c patientdb

CREATE OR REPLACE VIEW patients_visible AS
SELECT p.*
FROM "Patients" p
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    OR (COALESCE(current_setting('app.current_user_role', true), '') IN ('Provider', 'Nurse')
        AND p."FacilityId" IS NOT NULL
        AND p."FacilityId" = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                p."FacilityId"::VARCHAR
            )::UUID)
    OR p."CreatedBy" = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), 'none')
);

CREATE OR REPLACE VIEW patient_allergies_visible AS
SELECT a.*
FROM "Allergies" a
JOIN patients_visible pv ON a.patientid = pv.patientid;

CREATE OR REPLACE VIEW patient_conditions_visible AS
SELECT mc.*
FROM "MedicalConditions" mc
JOIN patients_visible pv ON mc.patientid = pv.patientid;

-- ============================================================================
-- SECTION 3: CLINICAL SERVICE — encounter isolation views (clinicaldb)
-- ============================================================================

\c clinicaldb

CREATE OR REPLACE VIEW encounters_visible AS
SELECT e.*
FROM "Encounters" e
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    OR (COALESCE(current_setting('app.current_user_role', true), '') = 'Provider'
        AND e.providerid = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '00000000-0000-0000-0000-000000000000'))
    OR (COALESCE(current_setting('app.current_user_role', true), '') = 'Nurse'
        AND e."FacilityId" IS NOT NULL
        AND e."FacilityId" = COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                '00000000-0000-0000-0000-000000000000'
            )::UUID)
);

CREATE OR REPLACE VIEW encounter_diagnoses_visible AS
SELECT d.*
FROM "EncounterDiagnoses" d
JOIN encounters_visible ev ON d.encounterid = ev.encounterid;

CREATE OR REPLACE VIEW encounter_procedures_visible AS
SELECT p.*
FROM "EncounterProcedures" p
JOIN encounters_visible ev ON p.encounterid = ev.encounterid;

CREATE OR REPLACE VIEW clinical_notes_visible AS
SELECT n.*
FROM "ClinicalNotes" n
JOIN encounters_visible ev ON n.encounterid = ev.encounterid;

CREATE OR REPLACE VIEW clinical_prescriptions_visible AS
SELECT p.*
FROM "Prescriptions" p
JOIN encounters_visible ev ON p.encounterid = ev.encounterid;

-- ============================================================================
-- SECTION 4: APPOINTMENT SERVICE — appointment isolation views (appointmentdb)
-- ============================================================================

\c appointmentdb

CREATE OR REPLACE VIEW appointments_visible AS
SELECT a.*
FROM "Appointments" a
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') = 'Admin'
    OR a.providerid = COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '00000000-0000-0000-0000-000000000000')
    OR (COALESCE(current_setting('app.current_user_role', true), '') IN ('Nurse', 'Receptionist')
        AND a.facilityid IS NOT NULL
        AND a.facilityid = (COALESCE(
                NULLIF(current_setting('app.current_facility_id', true), ''),
                '00000000-0000-0000-0000-000000000000'
            ))::UUID)
);

-- ============================================================================
-- SECTION 5: LAB SERVICE — lab isolation views (his_hope_lab)
-- ============================================================================

\c labdb

CREATE OR REPLACE VIEW lab_orders_visible AS
SELECT lo.*
FROM "LabOrders" lo
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'LabTechnician')
    OR lo.providerid = (COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '00000000-0000-0000-0000-000000000000'))::UUID
);

CREATE OR REPLACE VIEW lab_tests_visible AS
SELECT lt.*
FROM "LabTests" lt
JOIN lab_orders_visible lov ON lt.laborderid = lov.id;

CREATE OR REPLACE VIEW lab_results_visible AS
SELECT lr.*
FROM "LabResults" lr
JOIN lab_tests_visible ltv ON lr.labtestid = ltv.id;

-- ============================================================================
-- SECTION 6: BILLING SERVICE — billing isolation views (his_hope_billing)
-- ============================================================================

\c billingdb

CREATE OR REPLACE VIEW invoices_visible AS
SELECT i.*
FROM "Invoices" i
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'BillingClerk')
);

CREATE OR REPLACE VIEW payments_visible AS
SELECT p.*
FROM "Payments" p
JOIN invoices_visible iv ON p.invoiceid = iv.id;

-- ============================================================================
-- SECTION 7: PHARMACY SERVICE — prescription isolation views (his_hope_pharmacy)
-- ============================================================================

\c pharmacydb

CREATE OR REPLACE VIEW prescriptions_visible AS
SELECT p.*
FROM "Prescriptions" p
WHERE (
    COALESCE(current_setting('app.current_user_role', true), '') IN ('Admin', 'Pharmacist')
    OR p.providerid = (COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), '00000000-0000-0000-0000-000000000000'))::UUID
);

-- Switch back
\c postgres

-- ============================================================================
-- SECTION 8: Usage examples
-- ============================================================================
-- Every API request must set the security context BEFORE executing queries:
--
--   SET app.current_user_id = '00000000-0000-0000-0000-000000000101';
--   SET app.current_user_role = 'Admin';
--   SET app.current_facility_id = '11111111-1111-1111-1111-111111111111';
--
-- EF Core should query the views instead of base tables for reads:
--   modelBuilder.Entity<Patient>().ToView("patients_visible");
--   modelBuilder.Entity<Appointment>().ToView("appointments_visible");
--   modelBuilder.Entity<Encounter>().ToView("encounters_visible");
--
-- The Npgsql DbConnectionInterceptor sets session variables automatically
-- on connection open.
-- ============================================================================
