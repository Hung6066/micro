-- ====================================================================
-- Migration 019: Add soft-delete columns to core entities
-- Adds deleted_at and deleted_by columns with filtered indexes
-- so queries can efficiently filter out soft-deleted records.
-- ====================================================================

-- -----------------------
-- patientdb.Patients
-- -----------------------
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS deleted_by UUID;

CREATE INDEX IF NOT EXISTS idx_patients_active_filtered
    ON patientdb.Patients (last_name, first_name)
    WHERE deleted_at IS NULL;

-- -----------------------
-- patientdb.Allergies
-- -----------------------
ALTER TABLE patientdb.Allergies ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE patientdb.Allergies ADD COLUMN IF NOT EXISTS deleted_by UUID;

CREATE INDEX IF NOT EXISTS idx_allergies_active_filtered
    ON patientdb.Allergies (patient_id, allergen)
    WHERE deleted_at IS NULL;

-- -----------------------
-- patientdb.MedicalConditions
-- -----------------------
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS deleted_by UUID;

CREATE INDEX IF NOT EXISTS idx_medicalconditions_active_filtered
    ON patientdb.MedicalConditions (patient_id, condition_name)
    WHERE deleted_at IS NULL;

-- -----------------------
-- appointmentdb.Appointments
-- -----------------------
ALTER TABLE appointmentdb.Appointments ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE appointmentdb.Appointments ADD COLUMN IF NOT EXISTS deleted_by UUID;

CREATE INDEX IF NOT EXISTS idx_appointments_active_filtered
    ON appointmentdb.Appointments (patient_id, scheduled_date)
    WHERE deleted_at IS NULL;

-- -----------------------
-- clinicaldb.Encounters
-- -----------------------
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS deleted_by UUID;

CREATE INDEX IF NOT EXISTS idx_encounters_active_filtered
    ON clinicaldb.Encounters (patient_id, encounter_date)
    WHERE deleted_at IS NULL;
