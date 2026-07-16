export interface Patient {
  id: string;
  fullName: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  dateOfBirth: string;
  age: number;
  genderCode: string;
  genderName: string;
  phone: string;
  email?: string;
  street: string;
  district: string;
  city: string;
  province: string;
  postalCode?: string;
  country: string;
  bloodTypeCode?: string;
  bloodTypeName?: string;
  raceCode?: string;
  maritalStatusCode?: string;
  insuranceId?: string;
  nationalId?: string;
  occupation?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  allergies: Allergy[];
  conditions: MedicalCondition[];
}

export interface Allergy {
  id: string;
  allergen: string;
  reaction?: string;
  severity?: string;
  recordedDate: string;
  isActive: boolean;
}

export interface MedicalCondition {
  id: string;
  conditionName: string;
  icd10Code?: string;
  onsetDate?: string;
  resolvedDate?: string;
  isChronic: boolean;
  notes?: string;
  isActive: boolean;
}

export interface CreatePatientRequest {
  firstName: string;
  lastName: string;
  middleName?: string;
  dateOfBirth: string;
  genderCode: string;
  phone: string;
  email?: string;
  street: string;
  district: string;
  city: string;
  province: string;
  postalCode?: string;
  country: string;
  insuranceId?: string;
  nationalId?: string;
}

export { PagedResult } from './paged-result.model';
