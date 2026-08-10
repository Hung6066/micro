import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class LifecycleService {
  private readonly baseUrl = `${environment.apiUrl}/resources`;

  constructor(private readonly http: HttpClient) {}

  start(name: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${encodeURIComponent(name)}/start`, {});
  }

  stop(name: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${encodeURIComponent(name)}/stop`, {});
  }

  restart(name: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${encodeURIComponent(name)}/restart`, {});
  }
}
