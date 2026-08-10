import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Patient, CreatePatientRequest } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import { Invoice } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class PatientService {
  private readonly baseUrl = `${environment.apiUrl}/patients`;

  private http = inject(HttpClient);

  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Patient>> {
    const params = new HttpParams()
      .set('q', query)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Patient>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getById(id: string): Observable<Patient> {
    return this.http.get<Patient>(`${this.baseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  create(request: CreatePatientRequest): Observable<Patient> {
    return this.http.post<Patient>(`${this.baseUrl}/`, request).pipe(
      catchError(this.handleError),
    );
  }

  update(id: string, request: CreatePatientRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/${id}`, request).pipe(
      catchError(this.handleError),
    );
  }

  deactivate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/deactivate`, {}).pipe(
      catchError(this.handleError),
    );
  }

  reactivate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/reactivate`, {}).pipe(
      catchError(this.handleError),
    );
  }

  // ── Cross-service convenience methods ──

  getEncounters(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    const params = new HttpParams()
      .set('patientId', patientId)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Encounter>>(`${this.baseUrl}/${patientId}/encounters`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getAppointments(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Appointment>>(`${this.baseUrl}/${patientId}/appointments`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getPrescriptions(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Prescription>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Prescription>>(`${this.baseUrl}/${patientId}/prescriptions`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getLabOrders(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<LabOrder>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<LabOrder>>(`${this.baseUrl}/${patientId}/lab-orders`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getInvoices(patientId: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Invoice>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Invoice>>(`${this.baseUrl}/${patientId}/invoices`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[PatientService]', errorMessage);
    return throwError(() => error);
  }
}
