import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { CriticalAlert } from '@core/models/critical-alert.model';
import { CriticalAlertRule, CriticalAlertRuleRequest } from '@core/models/critical-alert-rule.model';

@Injectable({ providedIn: 'root' })
export class LabCriticalAlertService {
  private readonly http = inject(HttpClient);
  private readonly alertsBaseUrl = `${environment.apiUrl}/critical-alerts`;
  private readonly rulesBaseUrl = `${environment.apiUrl}/critical-alert-rules`;

  listCriticalAlerts(): Observable<CriticalAlert[]> {
    return this.http.get<CriticalAlert[]>(this.alertsBaseUrl);
  }

  acknowledgeCriticalAlert(id: string): Observable<CriticalAlert> {
    return this.http.post<CriticalAlert>(`${this.alertsBaseUrl}/${id}/acknowledge`, {});
  }

  resolveCriticalAlert(id: string): Observable<CriticalAlert> {
    return this.http.post<CriticalAlert>(`${this.alertsBaseUrl}/${id}/resolve`, {});
  }

  listCriticalAlertRules(): Observable<CriticalAlertRule[]> {
    return this.http.get<CriticalAlertRule[]>(this.rulesBaseUrl);
  }

  saveCriticalAlertRule(rule: CriticalAlertRuleRequest): Observable<CriticalAlertRule> {
    const { id, ...payload } = rule;

    if (id) {
      return this.http.put<CriticalAlertRule>(`${this.rulesBaseUrl}/${id}`, payload);
    }

    return this.http.post<CriticalAlertRule>(this.rulesBaseUrl, payload);
  }

  deleteCriticalAlertRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.rulesBaseUrl}/${id}`);
  }
}
