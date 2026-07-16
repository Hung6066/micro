import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Encounter, StartEncounterRequest, RecordVitalsRequest, AddDiagnosisRequest } from '@core/models/encounter.model';
import { PagedResult } from '@core/models/paged-result.model';
import { mockEncounters } from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockClinicalService {
  private encountersSubject = new BehaviorSubject<Encounter[]>([...mockEncounters]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  list(page = 1, pageSize = 20): Observable<PagedResult<Encounter>> {
    const items = this.encountersSubject.value;
    const total = items.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Encounter> = {
      items: items.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Encounter>> {
    const filtered = this.encountersSubject.value.filter(
      (e) =>
        e.chiefComplaint?.toLowerCase().includes(query.toLowerCase()) ||
        e.assessment?.toLowerCase().includes(query.toLowerCase()) ||
        e.plan?.toLowerCase().includes(query.toLowerCase()) ||
        e.encounterType.toLowerCase().includes(query.toLowerCase()),
    );
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Encounter> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getById(id: string): Observable<Encounter> {
    const encounter = this.encountersSubject.value.find((e) => e.id === id);
    return of(encounter!).pipe(delay(this.delayMs()));
  }

  start(request: StartEncounterRequest): Observable<Encounter> {
    const newEncounter: Encounter = {
      id: `enc-${String(this.encountersSubject.value.length + 1).padStart(3, '0')}`,
      patientId: request.patientId,
      providerId: request.providerId,
      appointmentId: request.appointmentId,
      encounterDate: new Date().toISOString(),
      encounterType: request.encounterTypeCode,
      encounterTypeName: request.encounterTypeCode === 'consultation' ? 'Khám bệnh' : 'Tái khám',
      status: 'in_progress',
      statusName: 'Đang khám',
      chiefComplaint: '',
      diagnoses: [],
      vitalSigns: {},
      createdAt: new Date().toISOString(),
    };
    this.encountersSubject.next([...this.encountersSubject.value, newEncounter]);
    return of(newEncounter).pipe(delay(this.delayMs()));
  }

  recordVitals(id: string, request: RecordVitalsRequest): Observable<void> {
    const encounters = this.encountersSubject.value;
    const index = encounters.findIndex((e) => e.id === id);
    if (index !== -1) {
      encounters[index] = {
        ...encounters[index],
        vitalSigns: { ...request },
        updatedAt: new Date().toISOString(),
      };
      this.encountersSubject.next([...encounters]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  addDiagnosis(id: string, request: AddDiagnosisRequest): Observable<void> {
    const encounters = this.encountersSubject.value;
    const index = encounters.findIndex((e) => e.id === id);
    if (index !== -1) {
      encounters[index] = {
        ...encounters[index],
        diagnoses: [
          ...(encounters[index].diagnoses || []),
          {
            conditionName: request.conditionName,
            icd10Code: request.icd10Code,
            isPrimary: request.isPrimary,
            notes: request.notes,
          },
        ],
        updatedAt: new Date().toISOString(),
      };
      this.encountersSubject.next([...encounters]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  complete(id: string): Observable<void> {
    const encounters = this.encountersSubject.value;
    const index = encounters.findIndex((e) => e.id === id);
    if (index !== -1) {
      encounters[index] = {
        ...encounters[index],
        status: 'completed',
        statusName: 'Hoàn thành',
        updatedAt: new Date().toISOString(),
      };
      this.encountersSubject.next([...encounters]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }
}
