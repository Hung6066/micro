import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Appointment, ScheduleAppointmentRequest } from '@core/models/appointment.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AppointmentService {
  private readonly baseUrl = `${environment.apiUrl}/appointments`;

  constructor(private http: HttpClient) {}

  schedule(request: ScheduleAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(`${this.baseUrl}/`, request);
  }

  checkIn(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/checkin`, {});
  }

  checkOut(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/checkout`, {});
  }

  cancel(id: string, reason?: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/cancel`, { reason });
  }
}
