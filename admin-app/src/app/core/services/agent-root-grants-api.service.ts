import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export type AgentRootGrantStatus = "pending" | "issued" | "revoked" | "expired" | "failed";

/**
 * One agent root SSH grant (IdentityService /admin/iam/infra-access/agent-root-grants).
 * The signed certificate is never part of this model: only the workload fetcher gets it.
 */
export interface AgentRootGrant {
  id: string;
  agentId: string;
  reason: string;
  durationMinutes: number;
  status: AgentRootGrantStatus;
  requestedBy: string;
  requestedByEmail: string;
  requestedAt: string;
  approvedBy: string | null;
  approvedByEmail: string | null;
  approvedAt: string | null;
  validBefore: string | null;
  certificateSerial: string | null;
  revokedBy: string | null;
  revokedAt: string | null;
  failureReason: string | null;
  active: boolean;
  remainingMinutes: number;
  requestedByMe: boolean;
  pendingUntil: string | null;
}

export interface AgentRootGrantList {
  enabled: boolean;
  agentId: string;
  principal: string;
  minMinutes: number;
  maxMinutes: number;
  pendingTtlMinutes: number;
  items: AgentRootGrant[];
}

export interface CreateAgentRootGrantRequest {
  durationMinutes: number;
  reason: string;
}

@Injectable({ providedIn: "root" })
export class AgentRootGrantsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.adminApiUrl}/iam/infra-access/agent-root-grants`;

  /** Super-admin only; newest 100 grants. */
  list(): Observable<AgentRootGrantList> {
    return this.http.get<AgentRootGrantList>(this.baseUrl);
  }

  /** Needs a fresh MFA (403 step_up_required otherwise). */
  create(request: CreateAgentRootGrantRequest): Observable<AgentRootGrant> {
    return this.http.post<AgentRootGrant>(this.baseUrl, request);
  }

  /** Another super admin than the requester, with a fresh MFA. */
  approve(id: string): Observable<AgentRootGrant> {
    return this.http.post<AgentRootGrant>(`${this.baseUrl}/${encodeURIComponent(id)}/approve`, {});
  }

  revoke(id: string): Observable<AgentRootGrant> {
    return this.http.post<AgentRootGrant>(`${this.baseUrl}/${encodeURIComponent(id)}/revoke`, {});
  }
}
