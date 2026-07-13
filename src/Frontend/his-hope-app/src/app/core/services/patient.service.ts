import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Patient, CreatePatientRequest, PagedResult } from '@core/models/patient.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class PatientService {
  private readonly baseUrl = `${environment.apiUrl}/patients`;

  constructor(private http: HttpClient) {}

  search(query: string, page = 1, pageSize = 20): Observable<PagedResult<Patient>> {
    const params = new HttpParams()
      .set('q', query)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Patient>>(`${this.baseUrl}/search`, { params });
  }

  getById(id: string): Observable<Patient> {
    return this.http.get<Patient>(`${this.baseUrl}/${id}`);
  }

  create(request: CreatePatientRequest): Observable<Patient> {
    return this.http.post<Patient>(`${this.baseUrl}/`, request);
  }

  update(id: string, request: CreatePatientRequest): Observable<Patient> {
    return this.http.put<Patient>(`${this.baseUrl}/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  reactivate(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/reactivate`, {});
  }
}
