import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Resource } from '../models/resource.model';

@Injectable({ providedIn: 'root' })
export class ResourceService {
  private readonly baseUrl = `${environment.apiUrl}/resources`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Resource[]> {
    return this.http.get<Resource[]>(this.baseUrl);
  }

  getByName(name: string): Observable<Resource> {
    return this.http.get<Resource>(`${this.baseUrl}/${encodeURIComponent(name)}`);
  }

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
