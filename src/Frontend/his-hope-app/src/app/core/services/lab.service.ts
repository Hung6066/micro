import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { LabOrder, CreateLabOrderRequest, RecordLabResultRequest, LabOrderSearchParams } from '@core/models/lab-order.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class LabService {
  private readonly baseUrl = `${environment.apiUrl}/lab-orders`;

  constructor(private http: HttpClient) {}

  searchLabOrders(params?: LabOrderSearchParams): Observable<PagedResult<LabOrder>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.searchTerm) httpParams = httpParams.set('q', params.searchTerm);
      if (params.patientId) httpParams = httpParams.set('patientId', params.patientId);
      if (params.statusCode) httpParams = httpParams.set('statusCode', params.statusCode);
      if (params.page) httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }
    return this.http.get<PagedResult<LabOrder>>(`${this.baseUrl}/search`, { params: httpParams }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getLabOrder(id: string): Observable<LabOrder> {
    return this.http.get<LabOrder>(`${this.baseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createLabOrder(data: CreateLabOrderRequest): Observable<LabOrder> {
    return this.http.post<LabOrder>(`${this.baseUrl}/`, data).pipe(
      catchError(this.handleError),
    );
  }

  submitLabOrder(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/submit`, {}).pipe(
      catchError(this.handleError),
    );
  }

  collectSpecimen(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/collect`, {}).pipe(
      catchError(this.handleError),
    );
  }

  recordResult(id: string, data: RecordLabResultRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/result`, data).pipe(
      catchError(this.handleError),
    );
  }

  cancelLabOrder(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/cancel`, {}).pipe(
      catchError(this.handleError),
    );
  }

  getPatientLabOrders(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<LabOrder>> {
    const params = new HttpParams()
      .set('patientId', patientId)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<LabOrder>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[LabService]', errorMessage);
    return throwError(() => error);
  }
}
