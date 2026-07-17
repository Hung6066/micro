// @ts-nocheck
import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Medication, CreateMedicationRequest, UpdateMedicationRequest, MedicationSearchParams } from '@core/models/medication.model';
import { Prescription, CreatePrescriptionRequest, PrescriptionSearchParams } from '@core/models/prescription.model';
import { PagedResult } from '@core/models/paged-result.model';
import { mockMedications, mockPrescriptions, mockPatients, mockUsers } from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockPharmacyService {
  private medicationsSubject = new BehaviorSubject<Medication[]>([...mockMedications]);
  private prescriptionsSubject = new BehaviorSubject<Prescription[]>([...mockPrescriptions]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  // ─── Medication endpoints ───────────────────────────────────────────────

  searchMedications(params?: MedicationSearchParams): Observable<PagedResult<Medication>> {
    let filtered = this.medicationsSubject.value;
    if (params?.searchTerm) {
      const q = params.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (m) =>
          m.name.toLowerCase().includes(q) ||
          m.genericName.toLowerCase().includes(q) ||
          m.brandName.toLowerCase().includes(q),
      );
    }
    const page = params?.page || 1;
    const pageSize = params?.pageSize || 20;
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Medication> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getMedication(id: string): Observable<Medication> {
    const med = this.medicationsSubject.value.find((m) => m.id === id);
    return of(med!).pipe(delay(this.delayMs()));
  }

  createMedication(data: CreateMedicationRequest): Observable<Medication> {
    const newMed: Medication = {
      id: `med-${String(this.medicationsSubject.value.length + 1).padStart(3, '0')}`,
      name: data.name,
      genericName: data.genericName,
      brandName: data.brandName,
      dosageForm: data.dosageForm,
      strength: data.strength,
      route: data.route,
      requiresPrescription: data.requiresPrescription,
      isActive: true,
      createdAt: new Date().toISOString(),
    };
    this.medicationsSubject.next([...this.medicationsSubject.value, newMed]);
    return of(newMed).pipe(delay(this.delayMs()));
  }

  updateMedication(id: string, data: UpdateMedicationRequest): Observable<Medication> {
    const meds = this.medicationsSubject.value;
    const index = meds.findIndex((m) => m.id === id);
    if (index === -1) {
      return of({} as Medication).pipe(delay(this.delayMs()));
    }
    const updated: Medication = {
      ...meds[index],
      ...(data.name !== undefined ? { name: data.name } : {}),
      ...(data.genericName !== undefined ? { genericName: data.genericName } : {}),
      ...(data.brandName !== undefined ? { brandName: data.brandName } : {}),
      ...(data.dosageForm !== undefined ? { dosageForm: data.dosageForm } : {}),
      ...(data.strength !== undefined ? { strength: data.strength } : {}),
      ...(data.route !== undefined ? { route: data.route } : {}),
      ...(data.requiresPrescription !== undefined ? { requiresPrescription: data.requiresPrescription } : {}),
      updatedAt: new Date().toISOString(),
    };
    meds[index] = updated;
    this.medicationsSubject.next([...meds]);
    return of(updated).pipe(delay(this.delayMs()));
  }

  deactivateMedication(id: string): Observable<void> {
    const meds = this.medicationsSubject.value;
    const index = meds.findIndex((m) => m.id === id);
    if (index !== -1) {
      meds[index] = { ...meds[index], isActive: false, updatedAt: new Date().toISOString() };
      this.medicationsSubject.next([...meds]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  // ─── Prescription endpoints ─────────────────────────────────────────────

  searchPrescriptions(params?: PrescriptionSearchParams): Observable<PagedResult<Prescription>> {
    let filtered = this.prescriptionsSubject.value;
    if (params?.searchTerm) {
      const q = params.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (rx) =>
          rx.medicationName.toLowerCase().includes(q) ||
          (rx.patientName && rx.patientName.toLowerCase().includes(q)),
      );
    }
    if (params?.patientId) {
      filtered = filtered.filter((rx) => rx.patientId === params.patientId);
    }
    if (params?.statusCode) {
      filtered = filtered.filter((rx) => rx.statusCode === params.statusCode);
    }
    const page = params?.page || 1;
    const pageSize = params?.pageSize || 20;
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Prescription> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getPrescription(id: string): Observable<Prescription> {
    const rx = this.prescriptionsSubject.value.find((p) => p.id === id);
    return of(rx!).pipe(delay(this.delayMs()));
  }

  createPrescription(data: CreatePrescriptionRequest): Observable<Prescription> {
    const patient = mockPatients.find((p) => p.id === data.patientId);
    const provider = mockUsers.find((u) => u.id === data.providerId);
    const newRx: Prescription = {
      id: `rx-${String(this.prescriptionsSubject.value.length + 1).padStart(3, '0')}`,
      patientId: data.patientId,
      providerId: data.providerId,
      medicationId: data.medicationId,
      medicationName: '',
      strength: '',
      dosageForm: '',
      dosageInstructions: data.dosageInstructions,
      route: data.route,
      quantity: data.quantity,
      refills: data.refills,
      statusCode: 'active',
      statusName: 'Đang dùng',
      prescribedAt: new Date().toISOString(),
      createdAt: new Date().toISOString(),
      patientName: patient?.fullName,
      providerName: provider?.fullName,
    };
    // Fill medication details from mock data
    const med = this.medicationsSubject.value.find((m) => m.id === data.medicationId);
    if (med) {
      newRx.medicationName = med.name;
      newRx.strength = med.strength;
      newRx.dosageForm = med.dosageForm;
    }
    this.prescriptionsSubject.next([...this.prescriptionsSubject.value, newRx]);
    return of(newRx).pipe(delay(this.delayMs()));
  }

  fillPrescription(id: string): Observable<void> {
    const rxs = this.prescriptionsSubject.value;
    const index = rxs.findIndex((rx) => rx.id === id);
    if (index !== -1) {
      rxs[index] = {
        ...rxs[index],
        statusCode: 'filled',
        statusName: 'Đã cấp',
        filledAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      this.prescriptionsSubject.next([...rxs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  cancelPrescription(id: string): Observable<void> {
    const rxs = this.prescriptionsSubject.value;
    const index = rxs.findIndex((rx) => rx.id === id);
    if (index !== -1) {
      rxs[index] = {
        ...rxs[index],
        statusCode: 'cancelled',
        statusName: 'Đã hủy',
        updatedAt: new Date().toISOString(),
      };
      this.prescriptionsSubject.next([...rxs]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getPatientPrescriptions(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Prescription>> {
    const filtered = this.prescriptionsSubject.value.filter((rx) => rx.patientId === patientId);
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Prescription> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }
}
