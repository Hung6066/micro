// @ts-nocheck
export interface Medication {
  id: string;
  name: string;
  genericName: string;
  brandName: string;
  dosageForm: string;
  strength: string;
  route: string;
  requiresPrescription: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateMedicationRequest {
  name: string;
  genericName: string;
  brandName: string;
  dosageForm: string;
  strength: string;
  route: string;
  requiresPrescription: boolean;
}

export interface UpdateMedicationRequest {
  name?: string;
  genericName?: string;
  brandName?: string;
  dosageForm?: string;
  strength?: string;
  route?: string;
  requiresPrescription?: boolean;
}

export interface MedicationSearchParams {
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}
