export enum LabOrderStatus {
  Ordered = 'ordered',
  SpecimenCollected = 'specimen_collected',
  InProgress = 'in_progress',
  Completed = 'completed',
  Cancelled = 'cancelled',
}

export enum AbnormalFlag {
  None = 'none',
  Low = 'low',
  High = 'high',
  CriticallyLow = 'critically_low',
  CriticallyHigh = 'critically_high',
  Abnormal = 'abnormal',
}

export interface LabResult {
  labResultId: string;
  value: string;
  unit: string;
  referenceRange: string;
  abnormalFlagCode: string;
  abnormalFlagName: string;
  resultStatusCode: string;
  resultStatusName: string;
  resultedAt?: string;
  performedBy?: string;
  notes?: string;
}

export interface LabTest {
  id: string;
  testCode: string;
  testName: string;
  specimenType: string;
  statusCode: string;
  statusName: string;
  orderedAt: string;
  collectedAt?: string;
  completedAt?: string;
  result?: LabResult;
}

export interface LabOrder {
  id: string;
  patientId: string;
  providerId: string;
  encounterId?: string;
  orderDate: string;
  statusCode: string;
  statusName: string;
  priorityCode: string;
  priorityName: string;
  notes?: string;
  tests: LabTest[];
  // Extended fields for richer UI
  patientName?: string;
  providerName?: string;
}

export interface CreateLabOrderRequest {
  patientId: string;
  providerId: string;
  encounterId?: string;
  priorityCode: string;
  notes?: string;
  tests: {
    testCode: string;
    testName: string;
    specimenType: string;
  }[];
}

export interface RecordLabResultRequest {
  value: string;
  unit: string;
  referenceRange: string;
  abnormalFlagCode: string;
  notes?: string;
}

export interface LabOrderSearchParams {
  searchTerm?: string;
  patientId?: string;
  statusCode?: string;
  page?: number;
  pageSize?: number;
}
