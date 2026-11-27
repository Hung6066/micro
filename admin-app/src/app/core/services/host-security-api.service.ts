import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export type HostSecuritySeverity = "critical" | "warning" | "info";
export type HostSecurityCategory = "integrity" | "sudo" | "login" | "runtime" | "network";

export interface HostIntegrityChange {
  category: string;
  path: string;
  change: string;
  severity: HostSecuritySeverity;
  detail: string | null;
}

export interface HostSecurityAck {
  key: string;
  ackedBy: string;
  ackedAt: string;
  note: string | null;
}

/** One security event (IdentityService /admin/iam/host-security). */
export interface HostSecurityEvent {
  id: string;
  ackKey: string;
  timestamp: string;
  category: HostSecurityCategory;
  securityEvent: string;
  severity: HostSecuritySeverity;
  host: string;
  actor: string | null;
  summary: string;
  detail: string | null;
  fingerprint: string | null;
  changes: HostIntegrityChange[];
  ack: HostSecurityAck | null;
}

export interface HostSecurityEventsPage {
  items: HostSecurityEvent[];
  unacknowledged: Partial<Record<HostSecuritySeverity, number>>;
  truncated: boolean;
  unavailableSources: string[];
}

export interface HostIntegrityStatus {
  host: string;
  lastReportAt: string;
  lastEvent: string;
  severity: HostSecuritySeverity;
  driftCount: number;
  fingerprint: string | null;
  stale: boolean;
}

/** A drift approval (critical drift: requested by one super-admin, approved by another). */
export interface HostIntegrityApproval {
  key: string;
  host: string;
  fingerprint: string;
  status: "approved" | "pending_second_approver";
  severity: HostSecuritySeverity;
  reason: string;
  requestedBy: string;
  requestedAt: string;
  approvedBy: string | null;
  approvedAt: string | null;
}

export interface HostSecurityMeta {
  configured: boolean;
  categories: HostSecurityCategory[];
  severities: HostSecuritySeverity[];
  maxLimit: number;
  maxRangeDays: number;
  staleAfterMinutes: number;
}

export interface HostSecurityQuery {
  from?: string;
  to?: string;
  categories?: string[];
  minSeverity?: string;
  host?: string;
  text?: string;
  unacknowledgedOnly?: boolean;
  limit?: number;
}

@Injectable({ providedIn: "root" })
export class HostSecurityApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.adminApiUrl}/iam/host-security`;

  /** Super-admin only; 403 for anyone else. */
  getMeta(): Observable<HostSecurityMeta> {
    return this.http.get<HostSecurityMeta>(`${this.baseUrl}/meta`);
  }

  getEvents(query: HostSecurityQuery): Observable<HostSecurityEventsPage> {
    return this.http.get<HostSecurityEventsPage>(`${this.baseUrl}/events`, { params: toParams(query) });
  }

  getHosts(): Observable<{ items: HostIntegrityStatus[] }> {
    return this.http.get<{ items: HostIntegrityStatus[] }>(`${this.baseUrl}/hosts`);
  }

  getApprovals(): Observable<{ items: HostIntegrityApproval[] }> {
    return this.http.get<{ items: HostIntegrityApproval[] }>(`${this.baseUrl}/approvals`);
  }

  /**
   * Approve a drift so the host accepts it into its baseline (fresh MFA; 403 step_up_required
   * otherwise). Critical drift answers 202 pending_second_approver until another admin approves.
   */
  approve(host: string, fingerprint: string, reason: string): Observable<{ status: string; key: string }> {
    return this.http.post<{ status: string; key: string }>(`${this.baseUrl}/approvals`, { host, fingerprint, reason: reason.trim() });
  }

  acknowledge(events: HostSecurityEvent[], note: string): Observable<{ acknowledged: number; items: HostSecurityAck[] }> {
    const seen = new Set<string>();
    const items = events
      .filter((event) => !seen.has(event.ackKey) && seen.add(event.ackKey))
      .map((event) => ({ key: event.ackKey, severity: event.severity, host: event.host, securityEvent: event.securityEvent }));
    return this.http.post<{ acknowledged: number; items: HostSecurityAck[] }>(`${this.baseUrl}/acks`, {
      items,
      note: note.trim() || null,
    });
  }
}

export function toParams(query: HostSecurityQuery): HttpParams {
  let params = new HttpParams();
  if (query.from) params = params.set("from", query.from);
  if (query.to) params = params.set("to", query.to);
  if (query.categories?.length) params = params.set("categories", query.categories.join(","));
  if (query.minSeverity) params = params.set("minSeverity", query.minSeverity);
  if (query.host?.trim()) params = params.set("host", query.host.trim());
  if (query.text?.trim()) params = params.set("text", query.text.trim());
  if (query.unacknowledgedOnly) params = params.set("unacknowledgedOnly", "true");
  if (query.limit) params = params.set("limit", String(query.limit));
  return params;
}
