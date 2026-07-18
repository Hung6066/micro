import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Appointment, ScheduleAppointmentRequest } from '@core/models/appointment.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AppointmentService {
  private readonly baseUrl = `${environment.apiUrl}/appointments`;

  private http = inject(HttpClient);

  list(page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Appointment>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  search(query: string, page: number = 1, pageSize: number = 20): Observable<PagedResult<Appointment>> {
    const params = new HttpParams()
      .set('q', query)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Appointment>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getById(id: string): Observable<Appointment> {
    return this.http.get<Appointment>(`${this.baseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  schedule(request: ScheduleAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.baseUrl}/`, request).pipe(
      catchError(this.handleError),
    );
  }

  checkIn(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/checkin`, {}).pipe(
      catchError(this.handleError),
    );
  }

  checkOut(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/checkout`, {}).pipe(
      catchError(this.handleError),
    );
  }

  cancel(id: string, reason?: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/cancel`, { reason }).pipe(
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[AppointmentService]', errorMessage);
    return throwError(() => error);
  }
}
