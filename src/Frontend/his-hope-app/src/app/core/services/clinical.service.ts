import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Encounter, StartEncounterRequest, RecordVitalsRequest, AddDiagnosisRequest } from '@core/models/encounter.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class ClinicalService {
  private readonly baseUrl = `${environment.apiUrl}/encounters`;

  private http = inject(HttpClient);

  list(page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Encounter>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  search(query: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Encounter>> {
    const params = new HttpParams()
      .set('q', query)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Encounter>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getById(id: string): Observable<Encounter> {
    return this.http.get<Encounter>(`${this.baseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  start(request: StartEncounterRequest): Observable<Encounter> {
    return this.http.post<Encounter>(`${this.baseUrl}/`, request).pipe(
      catchError(this.handleError),
    );
  }

  recordVitals(id: string, request: RecordVitalsRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/vitals`, request).pipe(
      catchError(this.handleError),
    );
  }

  addDiagnosis(id: string, request: AddDiagnosisRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/diagnosis`, request).pipe(
      catchError(this.handleError),
    );
  }

  complete(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/complete`, {}).pipe(
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[ClinicalService]', errorMessage);
    return throwError(() => error);
  }
}
