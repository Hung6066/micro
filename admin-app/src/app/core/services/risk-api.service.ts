import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export type RiskBand = "low" | "medium" | "high";
export const RISK_BANDS: readonly RiskBand[] = ["low", "medium", "high"];

export interface RiskReason {
  code: string;
  points: number;
}

/** One scored sign-in event (IdentityService /admin/iam/risk/events). R0 is report-only. */
export interface RiskEvent {
  id: string;
  timestamp: string;
  userId: string | null;
  userName: string | null;
  /** login_success | login_failed | mfa_failed | risk_policy_would_step_up | risk_policy_would_deny */
  eventType: string;
  severity: string | null;
  /** /24 (IPv4) or /48 (IPv6); raw addresses are not stored. */
  ipPrefix: string | null;
  score: number | null;
  band: RiskBand | null;
  reasons: RiskReason[];
  methods: string[];
  outcome: string | null;
}

export interface RiskEventsPage {
  items: RiskEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RiskWindowSummary {
  bands: Record<RiskBand, number>;
  total: number;
  wouldStepUp: number;
  wouldDeny: number;
}

export interface RiskSummary {
  generatedAt: string;
  reportOnly: boolean;
  windows: { "24h": RiskWindowSummary; "7d": RiskWindowSummary };
}

export interface RiskEventsQuery {
  band?: RiskBand | "";
  user?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: "root" })
export class RiskApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.adminApiUrl}/iam/risk`;

  /** Super-admin only; 403 for anyone else. Every read is audited server-side. */
  getEvents(query: RiskEventsQuery): Observable<RiskEventsPage> {
    return this.http.get<RiskEventsPage>(`${this.baseUrl}/events`, { params: toRiskParams(query) });
  }

  getSummary(): Observable<RiskSummary> {
    return this.http.get<RiskSummary>(`${this.baseUrl}/summary`);
  }
}

export function toRiskParams(query: RiskEventsQuery): HttpParams {
  let params = new HttpParams();
  if (query.band) params = params.set("band", query.band);
  if (query.user?.trim()) params = params.set("user", query.user.trim());
  if (query.from) params = params.set("from", query.from);
  if (query.to) params = params.set("to", query.to);
  if (query.page) params = params.set("page", String(query.page));
  if (query.pageSize) params = params.set("pageSize", String(query.pageSize));
  return params;
}
