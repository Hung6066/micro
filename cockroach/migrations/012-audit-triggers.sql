-- ============================================================================
-- His.Hope EMR - Audit Triggers for PHI Tables (HIPAA 164.312(b))
-- Version: 012
-- Description: Creates an audit_log table for tracking all PHI data
--              modifications across services. CockroachDB 24.1 has experimental
--              trigger support via 'experimental_enable_row_level_security'
--              but for reliability we use a dual approach:
--                1. Application-level audit via outbox pattern (in EF Core
--                   interceptors — see Shared/Infrastructure/AuditInterceptor.cs)
--                2. SQL-level audit_log table + helper functions for
--                   direct SQL/back-office auditing
-- Idempotent: uses IF NOT EXISTS for all creates.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Global audit_log table (in identitydb for centralized access)
-- ============================================================================

CREATE TABLE IF NOT EXISTS identitydb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,           -- INSERT, UPDATE, DELETE
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    facility_id UUID,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

-- Indexes for audit query performance
CREATE INDEX IF NOT EXISTS idx_audit_log_table_record ON identitydb.audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_changed_by ON identitydb.audit_log (changed_by);
CREATE INDEX IF NOT EXISTS idx_audit_log_changed_at ON identitydb.audit_log (changed_at);
CREATE INDEX IF NOT EXISTS idx_audit_log_action ON identitydb.audit_log (action);
CREATE INDEX IF NOT EXISTS idx_audit_log_facility ON identitydb.audit_log (facility_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_correlation ON identitydb.audit_log (correlation_id);

-- ============================================================================
-- SECTION 2: Helper function — insert audit entry
-- ============================================================================
-- Called by application code (EF Core interceptor) and can also be called
-- from stored procedures for back-office operations.

CREATE OR REPLACE FUNCTION identitydb.sp_insert_audit_log(
    p_table_name STRING(100),
    p_record_id UUID,
    p_action STRING(20),
    p_old_values JSONB,
    p_new_values JSONB,
    p_facility_id UUID DEFAULT NULL,
    p_correlation_id STRING(100) DEFAULT NULL,
    p_ip_address STRING(45) DEFAULT NULL,
    p_user_agent STRING(500) DEFAULT NULL
)
RETURNS UUID AS $$
DECLARE
    v_id UUID;
    v_user_id STRING(100);
    v_user_role STRING(50);
BEGIN
    -- Get session context set by application at connection time
    v_user_id := COALESCE(NULLIF(current_setting('app.current_user_id', true), ''), 'system');
    v_user_role := COALESCE(NULLIF(current_setting('app.current_user_role', true), ''), 'System');

    INSERT INTO identitydb.audit_log (
        table_name, record_id, action, changed_by, changed_by_role,
        old_values, new_values, facility_id, correlation_id, ip_address, user_agent,
        changed_at
    ) VALUES (
        p_table_name, p_record_id, p_action, v_user_id, v_user_role,
        p_old_values, p_new_values, p_facility_id, p_correlation_id, p_ip_address, p_user_agent,
        now()
    )
    RETURNING id INTO v_id;

    RETURN v_id;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- SECTION 3: Per-service audit_log tables (for local auditing in each service)
-- ============================================================================
-- Each service database also gets its own audit_log for local querying.
-- These replicate the same schema as the global one but are local to the
-- service database for performance and data locality.

CREATE TABLE IF NOT EXISTS patientdb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    facility_id UUID,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_patient_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_patient_audit_log_table_record ON patientdb.audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_patient_audit_log_changed_by ON patientdb.audit_log (changed_by);
CREATE INDEX IF NOT EXISTS idx_patient_audit_log_changed_at ON patientdb.audit_log (changed_at);

CREATE TABLE IF NOT EXISTS clinicaldb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    encounter_id UUID,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_clinical_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_clinical_audit_log_table_record ON clinicaldb.audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_clinical_audit_log_encounter ON clinicaldb.audit_log (encounter_id);
CREATE INDEX IF NOT EXISTS idx_clinical_audit_log_changed_at ON clinicaldb.audit_log (changed_at);

CREATE TABLE IF NOT EXISTS labdb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_lab_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_lab_audit_log_table_record ON labdb.audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_lab_audit_log_changed_by ON labdb.audit_log (changed_by);

CREATE TABLE IF NOT EXISTS billingdb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_billing_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_billing_audit_log_table_record ON billingdb.audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_billing_audit_log_changed_by ON billingdb.audit_log (changed_by);

CREATE TABLE IF NOT EXISTS pharmacydb.audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name STRING(100) NOT NULL,
    record_id UUID NOT NULL,
    action STRING(20) NOT NULL,
    changed_by STRING(100),
    changed_by_role STRING(50),
    changed_at TIMESTAMPTZ DEFAULT now(),
    old_values JSONB,
    new_values JSONB,
    correlation_id STRING(100),
    ip_address STRING(45),
    user_agent STRING(500),
    CONSTRAINT chk_pharmacy_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_pharmacy_audit_log_table_record ON pharmacydb.audit_log (table_name, record_id);

-- ============================================================================
-- SECTION 4: PHI table markers — which tables require audit logging
-- ============================================================================
-- These are the PHI-sensitive tables that REQUIRE audit logging per HIPAA:
--
--   PATIENT SERVICE:      Patients, Allergies, MedicalConditions
--   CLINICAL SERVICE:     Encounters, EncounterDiagnoses, EncounterProcedures,
--                         ClinicalNotes, Prescriptions
--   LAB SERVICE:          LabOrders, LabTests, LabResults
--   PHARMACY SERVICE:     Prescriptions
--   BILLING SERVICE:      Invoices, InvoiceLineItems, Payments
--   IDENTITY SERVICE:     AspNetUsers, AspNetUserRoles (audit access changes)
--
-- The application-level AuditInterceptor (Shared/Infrastructure) will
-- automatically log all INSERT, UPDATE, and DELETE operations on these tables
-- using the sp_insert_audit_log function defined above.
--
-- For direct SQL auditing without the application layer, CockroachDB
-- changefeeds can be configured to stream all mutations:
--
--   CREATE CHANGEFEED FOR TABLE patientdb.Patients, clinicaldb.Encounters
--   INTO 'kafka://...' WITH updated, resolved;
--
-- ============================================================================

-- ============================================================================
-- SECTION 5: Audit cleanup policy
-- ============================================================================
-- Audit logs are retained for a minimum of 6 years per HIPAA requirements.
-- A retention cleanup job should be scheduled separately:
--
--   DELETE FROM identitydb.audit_log
--   WHERE changed_at < now() - INTERVAL '6 years';
--
-- Consider partitioning by month for efficient archival.
-- ============================================================================

