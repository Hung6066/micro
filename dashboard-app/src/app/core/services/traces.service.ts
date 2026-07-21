import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TraceSummary, TraceDetail } from '../models/trace.model';

@Injectable({ providedIn: 'root' })
export class TracesService {
  private readonly baseUrl = `${environment.apiUrl}/traces`;

  constructor(private readonly http: HttpClient) {}

  search(params?: {
    service?: string;
    from?: string;
    to?: string;
    minDurationMs?: number;
    limit?: number;
  }): Observable<TraceSummary[]> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.service) httpParams = httpParams.set('service', params.service);
      if (params.from) httpParams = httpParams.set('from', params.from);
      if (params.to) httpParams = httpParams.set('to', params.to);
      if (params.minDurationMs) httpParams = httpParams.set('minDurationMs', params.minDurationMs.toString());
      if (params.limit) httpParams = httpParams.set('limit', params.limit.toString());
    }
    return this.http.get<TraceSummary[]>(this.baseUrl, { params: httpParams });
  }

  getById(traceId: string): Observable<TraceDetail> {
    return this.http.get<TraceDetail>(`${this.baseUrl}/${encodeURIComponent(traceId)}`);
  }
}
