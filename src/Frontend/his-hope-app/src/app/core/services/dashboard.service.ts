import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { environment } from '@env/environment';

export interface DashboardStats {
  totalPatients: number;
  todayAppointments: number;
  activeEncounters: number;
  pendingDiagnoses: number;
  // Augmented fields
  pendingLabs: number;
  outstandingInvoices: number;
  lowStockMedications: number;
  // Trend fields
  newPatientsToday: number;
  appointmentsTomorrow: number;
  recentEncounters: Encounter[];
  upcomingAppointments: Appointment[];
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly baseUrl = `${environment.apiUrl}/dashboard`;

  private http = inject(HttpClient);

  getStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.baseUrl}/stats`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getRecentEncounters(limit: number = 5): Observable<{ items: Encounter[] }> {
    return this.http.get<{ items: Encounter[] }>(`${this.baseUrl}/recent-encounters?limit=${limit}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getUpcomingAppointments(): Observable<{ items: Appointment[] }> {
    return this.http.get<{ items: Appointment[] }>(`${this.baseUrl}/upcoming-appointments`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[DashboardService]', errorMessage);
    return throwError(() => error);
  }
}
