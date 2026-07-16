import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Medication, CreateMedicationRequest, UpdateMedicationRequest, MedicationSearchParams } from '@core/models/medication.model';
import { Prescription, CreatePrescriptionRequest, PrescriptionSearchParams } from '@core/models/prescription.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class PharmacyService {
  private readonly medBaseUrl = `${environment.apiUrl}/medications`;
  private readonly rxBaseUrl = `${environment.apiUrl}/prescriptions`;

  constructor(private http: HttpClient) {}

  // ─── Medication endpoints ───────────────────────────────────────────────

  searchMedications(params?: MedicationSearchParams): Observable<PagedResult<Medication>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.searchTerm) httpParams = httpParams.set('q', params.searchTerm);
      if (params.page) httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }
    return this.http.get<PagedResult<Medication>>(`${this.medBaseUrl}/search`, { params: httpParams }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getMedication(id: string): Observable<Medication> {
    return this.http.get<Medication>(`${this.medBaseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createMedication(data: CreateMedicationRequest): Observable<Medication> {
    return this.http.post<Medication>(`${this.medBaseUrl}/`, data).pipe(
      catchError(this.handleError),
    );
  }

  updateMedication(id: string, data: UpdateMedicationRequest): Observable<Medication> {
    return this.http.put<Medication>(`${this.medBaseUrl}/${id}`, data).pipe(
      catchError(this.handleError),
    );
  }

  deactivateMedication(id: string): Observable<void> {
    return this.http.patch<void>(`${this.medBaseUrl}/${id}/deactivate`, {}).pipe(
      catchError(this.handleError),
    );
  }

  // ─── Prescription endpoints ─────────────────────────────────────────────

  searchPrescriptions(params?: PrescriptionSearchParams): Observable<PagedResult<Prescription>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.searchTerm) httpParams = httpParams.set('q', params.searchTerm);
      if (params.patientId) httpParams = httpParams.set('patientId', params.patientId);
      if (params.statusCode) httpParams = httpParams.set('statusCode', params.statusCode);
      if (params.page) httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }
    return this.http.get<PagedResult<Prescription>>(`${this.rxBaseUrl}/search`, { params: httpParams }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getPrescription(id: string): Observable<Prescription> {
    return this.http.get<Prescription>(`${this.rxBaseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createPrescription(data: CreatePrescriptionRequest): Observable<Prescription> {
    return this.http.post<Prescription>(`${this.rxBaseUrl}/`, data).pipe(
      catchError(this.handleError),
    );
  }

  fillPrescription(id: string): Observable<void> {
    return this.http.post<void>(`${this.rxBaseUrl}/${id}/fill`, {}).pipe(
      catchError(this.handleError),
    );
  }

  cancelPrescription(id: string): Observable<void> {
    return this.http.post<void>(`${this.rxBaseUrl}/${id}/cancel`, {}).pipe(
      catchError(this.handleError),
    );
  }

  getPatientPrescriptions(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Prescription>> {
    const params = new HttpParams()
      .set('patientId', patientId)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Prescription>>(`${this.rxBaseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  // ─── Error handler ──────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[PharmacyService]', errorMessage);
    return throwError(() => error);
  }
}
