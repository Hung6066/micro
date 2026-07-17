-- ============================================================================
-- His.Hope EMR - Audit Triggers for PHI Tables (HIPAA 164.312(b))
-- Version: pg-012
-- Description: Creates audit_log tables in each service database for tracking
--              all PHI data modifications. Uses application-level auditing via
--              EF Core interceptors + stored procedure for consistency.
--
--              Per HIPAA 164.312(b), all access to PHI must be logged with:
--              - User ID and role
--              - Timestamp
--              - Action (INSERT/UPDATE/DELETE)
--              - Old and new values
--              - IP address and correlation ID for request tracing
--
-- Idempotent: uses IF NOT EXISTS for all creates.
-- Compatible with: PostgreSQL 16+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Enable pgcrypto for gen_random_uuid() if not already enabled
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================================
-- SECTION 2: Per-service audit_log tables (local auditing in each service)
-- NOTE: identitydb already has "AuditLogs" from pg-010 with a simpler schema.
--       The per-service tables use a richer HIPAA-compliant schema.
-- ============================================================================

-- Patient Service
\c patientdb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    facilityid UUID,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_patient_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_patient_auditlogs_table_record ON "AuditLogs"(tablename, recordid);
CREATE INDEX IF NOT EXISTS idx_patient_auditlogs_changed_by ON "AuditLogs"(changedby);
CREATE INDEX IF NOT EXISTS idx_patient_auditlogs_changed_at ON "AuditLogs"(changedat);

-- Appointment Service
\c appointmentdb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    facilityid UUID,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_appt_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_appt_auditlogs_table_record ON "AuditLogs"(tablename, recordid);
CREATE INDEX IF NOT EXISTS idx_appt_auditlogs_changed_at ON "AuditLogs"(changedat);

-- Clinical Service
\c clinicaldb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    encounterid UUID,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_clinical_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_clinical_auditlogs_table_record ON "AuditLogs"(tablename, recordid);
CREATE INDEX IF NOT EXISTS idx_clinical_auditlogs_encounter ON "AuditLogs"(encounterid);
CREATE INDEX IF NOT EXISTS idx_clinical_auditlogs_changed_at ON "AuditLogs"(changedat);

-- Lab Service
\c labdb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_lab_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_lab_auditlogs_table_record ON "AuditLogs"(tablename, recordid);
CREATE INDEX IF NOT EXISTS idx_lab_auditlogs_changed_by ON "AuditLogs"(changedby);

-- Billing Service
\c billingdb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_billing_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_billing_auditlogs_table_record ON "AuditLogs"(tablename, recordid);
CREATE INDEX IF NOT EXISTS idx_billing_auditlogs_changed_by ON "AuditLogs"(changedby);

-- Pharmacy Service
\c pharmacydb

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tablename VARCHAR(100) NOT NULL,
    recordid UUID NOT NULL,
    action VARCHAR(20) NOT NULL,
    changedby VARCHAR(100),
    changedbyrole VARCHAR(50),
    changedat TIMESTAMPTZ DEFAULT now(),
    oldvalues JSONB,
    newvalues JSONB,
    correlationid VARCHAR(100),
    ipaddress VARCHAR(45),
    useragent VARCHAR(500),
    CONSTRAINT chk_pharmacy_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IF NOT EXISTS idx_pharmacy_auditlogs_table_record ON "AuditLogs"(tablename, recordid);

-- Switch back
\c postgres

-- ============================================================================
-- SECTION 4: SEED DATA — Sample audit log entries
-- ============================================================================
-- These demonstrate realistic audit data showing PHI access history for
-- testing and development. In production, these are populated automatically
-- by the AuditInterceptor.

-- 4a. Identity service — user creation audit (using existing pg-010 AuditLogs schema)
\c identitydb

INSERT INTO "AuditLogs" (id, userid, username, action, resourcetype, resourceid, details, ipaddress, "timestamp") VALUES
('00000000-0000-0000-0000-a00000000001', '00000000-0000-0000-0000-000000000000', 'system',
 'INSERT', 'AspNetUsers', '00000000-0000-0000-0000-000000000101',
 'Created admin: Quản Trị Viên (admin@hishop.vn)', '127.0.0.1', '2026-07-01 08:00:00+00'),
('00000000-0000-0000-0000-a00000000002', '00000000-0000-0000-0000-000000000000', 'system',
 'INSERT', 'AspNetUsers', '00000000-0000-0000-0000-000000000102',
 'Created user: Nguyễn Văn Minh (bacsy.nguyen@hishop.vn)', '127.0.0.1', '2026-07-01 08:01:00+00'),
('00000000-0000-0000-0000-a00000000003', '00000000-0000-0000-0000-000000000000', 'system',
 'INSERT', 'AspNetUsers', '00000000-0000-0000-0000-000000000103',
 'Created user: Trần Thị Lan (bacsy.tran@hishop.vn)', '127.0.0.1', '2026-07-01 08:02:00+00'),
('00000000-0000-0000-0000-a00000000004', '00000000-0000-0000-0000-000000000000', 'system',
 'INSERT', 'AspNetUsers', '00000000-0000-0000-0000-000000000104',
 'Created user: Lê Thị Hồng (dieuduong.le@hishop.vn)', '127.0.0.1', '2026-07-01 08:03:00+00')
ON CONFLICT (id) DO NOTHING;

-- 4b. Patient service — patient creation audit
\c patientdb

INSERT INTO "AuditLogs" (id, tablename, recordid, action, changedby, changedbyrole,
    changedat, newvalues, facilityid, correlationid, ipaddress) VALUES
('00000000-0000-0000-0000-b00000000001', 'Patients', '00000000-0000-0000-0000-000000000001', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-01 08:05:00+00',
 '{"FirstName":"A","LastName":"Nguyễn","FullName":"Nguyễn Văn A"}',
 '11111111-1111-1111-1111-111111111111', 'corr-patient-001', '192.168.1.100'),
('00000000-0000-0000-0000-b00000000002', 'Patients', '00000000-0000-0000-0000-000000000002', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-01 08:06:00+00',
 '{"FirstName":"B","LastName":"Trần","FullName":"Trần Thị B"}',
 '11111111-1111-1111-1111-111111111111', 'corr-patient-002', '192.168.1.100'),
('00000000-0000-0000-0000-b00000000003', 'Allergies', '00000000-0000-0000-0000-000000000011', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-01 08:10:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","Allergen":"Penicillin","Severity":"Moderate"}',
 '11111111-1111-1111-1111-111111111111', 'corr-allergy-001', '192.168.1.100'),
('00000000-0000-0000-0000-b00000000004', 'MedicalConditions', '00000000-0000-0000-0000-000000000021', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-01 08:15:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","ConditionName":"Tăng huyết áp nguyên phát","ICD10":"I10"}',
 '11111111-1111-1111-1111-111111111111', 'corr-condition-001', '192.168.1.100')
ON CONFLICT (id) DO NOTHING;

-- 4c. Clinical service — encounter creation audit
\c clinicaldb

INSERT INTO "AuditLogs" (id, tablename, recordid, action, changedby, changedbyrole,
    changedat, newvalues, encounterid, correlationid, ipaddress) VALUES
('00000000-0000-0000-0000-c00000000001', 'Encounters', '00000000-0000-0000-0000-000000000301', 'INSERT',
 '00000000-0000-0000-0000-000000000102', 'Provider', '2026-07-15 08:05:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","ChiefComplaint":"Đau đầu, chóng mặt"}',
 '00000000-0000-0000-0000-000000000301', 'corr-enc-301', '10.0.0.15'),
('00000000-0000-0000-0000-c00000000002', 'Encounters', '00000000-0000-0000-0000-000000000306', 'INSERT',
 '00000000-0000-0000-0000-000000000103', 'Provider', '2026-07-15 15:00:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000007","ChiefComplaint":"Khám sức khỏe tổng quát"}',
 '00000000-0000-0000-0000-000000000306', 'corr-enc-306', '10.0.0.16'),
('00000000-0000-0000-0000-c00000000003', 'Encounters', '00000000-0000-0000-0000-000000000306', 'UPDATE',
 '00000000-0000-0000-0000-000000000103', 'Provider', '2026-07-15 15:40:00+00',
 '{"Status":"COMPLETED","Plan":"Atorvastatin 10mg"}',
 '00000000-0000-0000-0000-000000000306', 'corr-enc-306', '10.0.0.16')
ON CONFLICT (id) DO NOTHING;

-- 4d. Billing service — invoice creation and payment audit
\c billingdb

INSERT INTO "AuditLogs" (id, tablename, recordid, action, changedby, changedbyrole,
    changedat, newvalues, correlationid, ipaddress) VALUES
('00000000-0000-0000-0000-d00000000001', 'Invoices', '00000000-0000-0000-0000-000000000501', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-15 08:30:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","Total":1120000,"Status":"Paid"}',
 'corr-inv-501', '10.0.0.20'),
('00000000-0000-0000-0000-d00000000002', 'Payments', '00000000-0000-0000-0000-000000000551', 'INSERT',
 '00000000-0000-0000-0000-000000000101', 'Admin', '2026-07-15 11:30:00+00',
 '{"InvoiceId":"00000000-0000-0000-0000-000000000501","Amount":1120000,"Method":"Cash"}',
 'corr-pay-551', '10.0.0.20')
ON CONFLICT (id) DO NOTHING;

-- 4e. Lab service — lab order and result audit
\c labdb

INSERT INTO "AuditLogs" (id, tablename, recordid, action, changedby, changedbyrole,
    changedat, newvalues, correlationid, ipaddress) VALUES
('00000000-0000-0000-0000-e00000000001', 'LabOrders', '00000000-0000-0000-0000-000000000401', 'INSERT',
 '00000000-0000-0000-0000-000000000102', 'Provider', '2026-07-15 08:15:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","Test":"CBC"}',
 'corr-lab-401', '10.0.0.15'),
('00000000-0000-0000-0000-e00000000002', 'LabResults', '00000000-0000-0000-0000-000000000461', 'INSERT',
 '00000000-0000-0000-0000-000000000104', 'LabTechnician', '2026-07-15 11:00:00+00',
 '{"TestId":"00000000-0000-0000-0000-000000000411","Value":"7.2","Unit":"10^3/uL","Status":"Normal"}',
 'corr-lab-411', '10.0.0.30')
ON CONFLICT (id) DO NOTHING;

-- 4f. Pharmacy service — prescription audit
\c pharmacydb

INSERT INTO "AuditLogs" (id, tablename, recordid, action, changedby, changedbyrole,
    changedat, newvalues, correlationid, ipaddress) VALUES
('00000000-0000-0000-0000-f00000000001', 'Prescriptions', '00000000-0000-0000-0000-000000000631', 'INSERT',
 '00000000-0000-0000-0000-000000000102', 'Provider', '2026-07-15 08:30:00+00',
 '{"PatientId":"00000000-0000-0000-0000-000000000001","Medication":"Amlodipine 5mg","Quantity":30}',
 'corr-rx-631', '10.0.0.15'),
('00000000-0000-0000-0000-f00000000002', 'Prescriptions', '00000000-0000-0000-0000-000000000634', 'UPDATE',
 '00000000-0000-0000-0000-000000000103', 'Pharmacist', '2026-07-15 16:00:00+00',
 '{"Status":"FILLED","FilledDate":"2026-07-15T16:00:00Z"}',
 'corr-rx-634', '10.0.0.35')
ON CONFLICT (id) DO NOTHING;

-- Switch back to postgres
\c postgres

-- ============================================================================
-- SECTION 6: PHI tables that require audit logging (HIPAA reference)
-- ============================================================================
-- Per HIPAA 164.312(b), the following PHI-sensitive tables MUST be audited:
--
--   PATIENT SERVICE:      Patients, Allergies, MedicalConditions
--   CLINICAL SERVICE:     Encounters, EncounterDiagnoses, EncounterProcedures,
--                         ClinicalNotes, Prescriptions
--   LAB SERVICE:          LabOrders, LabTests, LabResults
--   PHARMACY SERVICE:     Prescriptions
--   BILLING SERVICE:      Invoices, InvoiceLineItems, Payments
--   IDENTITY SERVICE:     AspNetUsers, AspNetUserRoles
--
-- The EF Core AuditInterceptor automatically logs all INSERT, UPDATE, DELETE
-- operations on these tables. See Shared/Infrastructure/AuditInterceptor.cs.
--
-- Audit retention: minimum 6 years per HIPAA. A cleanup job should be scheduled:
--
--   DELETE FROM "AuditLogs" WHERE "ChangedAt" < now() - INTERVAL '6 years';
--
-- Consider table partitioning by month for efficient archival and querying.
-- ============================================================================

-- ============================================================================
-- SECTION 7: Verification queries
-- ============================================================================
-- After running this migration, verify with:
--
--   -- Count total audit entries
--   SELECT COUNT(*) AS total FROM "AuditLogs";
--
--   -- Recent PHI access by user
--   SELECT "ChangedBy", "TableName", "Action", "ChangedAt"
--   FROM "AuditLogs" ORDER BY "ChangedAt" DESC LIMIT 10;
--
--   -- Audit trail for a specific patient (by correlation_id)
--   SELECT * FROM "AuditLogs"
--   WHERE "CorrelationId" LIKE 'corr-patient-001%'
--   ORDER BY "ChangedAt";
-- ============================================================================
