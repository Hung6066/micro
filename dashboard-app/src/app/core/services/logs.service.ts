import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LogEntry } from '../models/log-entry.model';

@Injectable({ providedIn: 'root' })
export class LogsService {
  private readonly baseUrl = `${environment.apiUrl}/logs`;

  constructor(private readonly http: HttpClient) {}

  query(params?: {
    service?: string;
    level?: string;
    from?: string;
    size?: number;
    query?: string;
    fromDate?: string;
    toDate?: string;
  }): Observable<LogEntry[]> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.service) httpParams = httpParams.set('service', params.service);
      if (params.level) httpParams = httpParams.set('level', params.level);
      if (params.from) httpParams = httpParams.set('from', params.from);
      if (params.size) httpParams = httpParams.set('size', params.size.toString());
      if (params.query) httpParams = httpParams.set('query', params.query);
      if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
      if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    }
    return this.http.get<LogEntry[]>(this.baseUrl, { params: httpParams });
  }
}
