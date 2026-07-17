// @ts-nocheck
import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay, map } from 'rxjs/operators';
import { Patient, CreatePatientRequest } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import { Invoice } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import {
  mockPatients,
  mockEncounters,
  mockAppointments,
  mockPrescriptions,
  mockLabOrders,
  mockInvoices,
} from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockPatientService {
  private patientsSubject = new BehaviorSubject<Patient[]>([...mockPatients]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Patient>> {
    const filtered = this.patientsSubject.value.filter(
      (p) =>
        p.fullName.toLowerCase().includes(query.toLowerCase()) ||
        p.phone.includes(query) ||
        (p.insuranceId && p.insuranceId.toLowerCase().includes(query.toLowerCase())) ||
        (p.nationalId && p.nationalId.includes(query)),
    );
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const items = filtered.slice(start, start + pageSize);
    const result: PagedResult<Patient> = {
      items,
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getById(id: string): Observable<Patient> {
    const patient = this.patientsSubject.value.find((p) => p.id === id);
    return of(patient!).pipe(delay(this.delayMs()));
  }

  create(request: CreatePatientRequest): Observable<Patient> {
    const newPatient: Patient = {
      id: `pat-${String(this.patientsSubject.value.length + 1).padStart(3, '0')}`,
      fullName: `${request.lastName} ${request.middleName ? request.middleName + ' ' : ''}${request.firstName}`,
      firstName: request.firstName,
      lastName: request.lastName,
      middleName: request.middleName,
      dateOfBirth: request.dateOfBirth,
      age: new Date().getFullYear() - new Date(request.dateOfBirth).getFullYear(),
      genderCode: request.genderCode,
      genderName: request.genderCode === 'M' ? 'Nam' : 'Nữ',
      phone: request.phone,
      email: request.email,
      street: request.street,
      district: request.district,
      city: request.city,
      province: request.province,
      postalCode: request.postalCode,
      country: request.country,
      insuranceId: request.insuranceId,
      nationalId: request.nationalId,
      isActive: true,
      createdAt: new Date().toISOString(),
      allergies: [],
      conditions: [],
    };
    this.patientsSubject.next([...this.patientsSubject.value, newPatient]);
    return of(newPatient).pipe(delay(this.delayMs()));
  }

  update(id: string, request: CreatePatientRequest): Observable<Patient> {
    const patients = this.patientsSubject.value;
    const index = patients.findIndex((p) => p.id === id);
    if (index === -1) {
      return of({} as Patient).pipe(delay(this.delayMs()));
    }
    const updated: Patient = {
      ...patients[index],
      firstName: request.firstName,
      lastName: request.lastName,
      middleName: request.middleName,
      dateOfBirth: request.dateOfBirth,
      genderCode: request.genderCode,
      genderName: request.genderCode === 'M' ? 'Nam' : 'Nữ',
      phone: request.phone,
      email: request.email,
      street: request.street,
      district: request.district,
      city: request.city,
      province: request.province,
      postalCode: request.postalCode,
      country: request.country,
      insuranceId: request.insuranceId,
      nationalId: request.nationalId,
      updatedAt: new Date().toISOString(),
    };
    patients[index] = updated;
    this.patientsSubject.next([...patients]);
    return of(updated).pipe(delay(this.delayMs()));
  }

  deactivate(id: string): Observable<void> {
    const patients = this.patientsSubject.value;
    const index = patients.findIndex((p) => p.id === id);
    if (index !== -1) {
      patients[index] = { ...patients[index], isActive: false, updatedAt: new Date().toISOString() };
      this.patientsSubject.next([...patients]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  reactivate(id: string): Observable<void> {
    const patients = this.patientsSubject.value;
    const index = patients.findIndex((p) => p.id === id);
    if (index !== -1) {
      patients[index] = { ...patients[index], isActive: true, updatedAt: new Date().toISOString() };
      this.patientsSubject.next([...patients]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getEncounters(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Encounter>> {
    const filtered = mockEncounters.filter((e) => e.patientId === patientId);
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getAppointments(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Appointment>> {
    const filtered = mockAppointments.filter((a) => a.patientId === patientId);
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getPrescriptions(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Prescription>> {
    const filtered = mockPrescriptions.filter((rx) => rx.patientId === patientId);
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getLabOrders(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<LabOrder>> {
    const filtered = mockLabOrders.filter((lab) => lab.patientId === patientId);
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getInvoices(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Invoice>> {
    const filtered = mockInvoices.filter((inv) => inv.patientId === patientId);
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  private paginate<T>(items: T[], page: number, pageSize: number): PagedResult<T> {
    const total = items.length;
    const start = (page - 1) * pageSize;
    return {
      items: items.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
  }
}
