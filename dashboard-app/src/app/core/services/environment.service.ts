import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EnvironmentContext } from '../models/environment.model';

@Injectable({ providedIn: 'root' })
export class EnvironmentService {
  private readonly baseUrl = `${environment.apiUrl}/environment`;

  constructor(private readonly http: HttpClient) {}

  getCurrent(): Observable<EnvironmentContext> {
    return this.http.get<EnvironmentContext>(this.baseUrl);
  }
}
