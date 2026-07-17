// @ts-nocheck
export interface VitalSigns {
  temperature?: number;
  heartRate?: number;
  respiratoryRate?: number;
  systolicBP?: number;
  diastolicBP?: number;
  oxygenSaturation?: number;
  heightCm?: number;
  weightKg?: number;
  bmi?: number;
}

export interface Diagnosis {
  conditionName: string;
  icd10Code: string;
  isPrimary: boolean;
  notes?: string;
}

export interface HistoryPresentIllness {
  onset?: string;
  location?: string;
  duration?: string;
  characteristics?: string;
  aggravatingFactors?: string;
  relievingFactors?: string;
  priorTreatments?: string;
}

export interface Encounter {
  id: string;
  patientId: string;
  providerId: string;
  appointmentId?: string;
  encounterDate: string;
  encounterType: string;
  encounterTypeName?: string;
  status: string;
  statusName?: string;
  chiefComplaint?: string;
  assessment?: string;
  plan?: string;
  diagnosisNotes?: string;
  vitalSigns?: VitalSigns;
  hpi?: HistoryPresentIllness;
  diagnoses: Diagnosis[];
  createdAt: string;
  updatedAt?: string;
}

export interface StartEncounterRequest {
  patientId: string;
  providerId: string;
  appointmentId?: string;
  encounterTypeCode: string;
}

export interface RecordVitalsRequest {
  temperature?: number;
  heartRate?: number;
  respiratoryRate?: number;
  systolicBP?: number;
  diastolicBP?: number;
  oxygenSaturation?: number;
  heightCm?: number;
  weightKg?: number;
  bmi?: number;
}

export interface AddDiagnosisRequest {
  conditionName: string;
  icd10Code: string;
  isPrimary: boolean;
  notes?: string;
}
