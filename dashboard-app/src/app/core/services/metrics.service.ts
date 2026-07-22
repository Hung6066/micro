import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MetricSnapshot } from '../models/metric-snapshot.model';

@Injectable({ providedIn: 'root' })
export class MetricsService {
  private readonly baseUrl = `${environment.apiUrl}/metrics`;

  constructor(private readonly http: HttpClient) {}

  getServiceMetrics(
    service: string,
    metrics: string[],
    range?: string
  ): Observable<MetricSnapshot[]> {
    let params = new HttpParams().set('service', service);
    metrics.forEach(m => {
      params = params.append('metrics', m);
    });
    if (range) params = params.set('range', range);
    return this.http.get<MetricSnapshot[]>(`${this.baseUrl}/service`, { params });
  }

  getSummary(): Observable<MetricSnapshot[]> {
    return this.http.get<MetricSnapshot[]>(`${this.baseUrl}/summary`);
  }
}
