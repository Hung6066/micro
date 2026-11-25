import { HttpClient, HttpParams, HttpResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

/** One infrastructure access event (IdentityService /admin/iam/infra-audit). */
export interface InfraAuditEvent {
  timestamp: string;
  source: string;
  actor: string | null;
  action: string;
  target: string | null;
  result: "allowed" | "denied" | "error" | "info";
  sourceIp: string | null;
  correlationId: string | null;
  detail: string | null;
  logType: string;
}

export interface InfraAuditPage {
  items: InfraAuditEvent[];
  nextTo: string | null;
}

export interface InfraAuditMeta {
  configured: boolean;
  sources: string[];
  results: string[];
  retentionDays: number;
  maxRangeDays: number;
  maxPageSize: number;
  maxExportRows: number;
}

export interface InfraAuditSummary {
  from: string;
  to: string;
  total: number;
  truncated: boolean;
  bySource: Record<string, number>;
  byResult: Record<string, number>;
  topActors: Array<{ key: string; value: number }>;
  topTargets: Array<{ key: string; value: number }>;
  perHour: Array<{ key: string; value: number }>;
}

export interface InfraAuditQuery {
  from?: string;
  to?: string;
  sources?: string[];
  actor?: string;
  text?: string;
  result?: string;
  limit?: number;
}

@Injectable({ providedIn: "root" })
export class InfraAuditApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  /** Super-admin only; 403 for anyone else. */
  getMeta(): Observable<InfraAuditMeta> {
    return this.http.get<InfraAuditMeta>(`${this.baseUrl}/iam/infra-audit/meta`);
  }

  getEvents(query: InfraAuditQuery): Observable<InfraAuditPage> {
    return this.http.get<InfraAuditPage>(`${this.baseUrl}/iam/infra-audit/events`, {
      params: toParams(query),
    });
  }

  getSummary(query: InfraAuditQuery): Observable<InfraAuditSummary> {
    return this.http.get<InfraAuditSummary>(`${this.baseUrl}/iam/infra-audit/summary`, {
      params: toParams({ from: query.from, to: query.to, sources: query.sources }),
    });
  }

  /** CSV download; the server requires a fresh MFA (403 step_up_required otherwise). */
  export(query: InfraAuditQuery): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/iam/infra-audit/export`, {
      params: toParams({ ...query, limit: undefined }),
      observe: "response",
      responseType: "blob",
    });
  }

  getGrantTimeline(grantId: string): Observable<InfraAuditPage & { grantId: string; from: string; to: string }> {
    return this.http.get<InfraAuditPage & { grantId: string; from: string; to: string }>(
      `${this.baseUrl}/iam/infra-audit/grants/${encodeURIComponent(grantId)}/timeline`,
    );
  }

  /** Any admin: their own activity only. */
  getMyEvents(query: InfraAuditQuery): Observable<InfraAuditPage> {
    return this.http.get<InfraAuditPage>(`${this.baseUrl}/infra-audit/me/events`, {
      params: toParams({ from: query.from, to: query.to, sources: query.sources, limit: query.limit }),
    });
  }
}

export function toParams(query: InfraAuditQuery): HttpParams {
  let params = new HttpParams();
  if (query.from) params = params.set("from", query.from);
  if (query.to) params = params.set("to", query.to);
  if (query.sources?.length) params = params.set("sources", query.sources.join(","));
  if (query.actor?.trim()) params = params.set("actor", query.actor.trim());
  if (query.text?.trim()) params = params.set("text", query.text.trim());
  if (query.result) params = params.set("result", query.result);
  if (query.limit) params = params.set("limit", String(query.limit));
  return params;
}
