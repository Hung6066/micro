import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SloResponse } from '../models/slo.model';

@Injectable({ providedIn: 'root' })
export class SloService {
  private readonly baseUrl = `${environment.apiUrl}/slo`;

  constructor(private readonly http: HttpClient) {}

  /** Fetch all SLO/SLI data for all services. */
  getAll(): Observable<SloResponse> {
    return this.http.get<SloResponse>(this.baseUrl);
  }
}
