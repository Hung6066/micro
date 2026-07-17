// @ts-nocheck
export enum PrescriptionStatus {
  Active = 'active',
  Filled = 'filled',
  PartiallyFilled = 'partially_filled',
  Cancelled = 'cancelled',
  Expired = 'expired',
}

export interface Prescription {
  id: string;
  patientId: string;
  providerId: string;
  medicationId: string;
  medicationName: string;
  strength: string;
  dosageForm: string;
  dosageInstructions: string;
  route: string;
  quantity: number;
  refills: number;
  statusCode: string;
  statusName: string;
  prescribedAt: string;
  filledAt?: string;
  createdAt: string;
  updatedAt?: string;
  // Extended fields beyond proto for richer UI
  patientName?: string;
  providerName?: string;
}

export interface PrescriptionItem {
  medicationId: string;
  medicationName: string;
  strength: string;
  dosageForm: string;
  dosageInstructions: string;
  route: string;
  quantity: number;
  refills: number;
}

export interface CreatePrescriptionRequest {
  patientId: string;
  providerId: string;
  medicationId: string;
  dosageInstructions: string;
  route: string;
  quantity: number;
  refills: number;
}

export interface PrescriptionSearchParams {
  searchTerm?: string;
  patientId?: string;
  statusCode?: string;
  page?: number;
  pageSize?: number;
}
