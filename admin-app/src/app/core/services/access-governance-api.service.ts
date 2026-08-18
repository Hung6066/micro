import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  IDENTITY_WORKBENCH_RESOURCES,
  identityWorkbenchActionPath,
  identityWorkbenchPath,
} from "../contracts/identity-workbench.naming";
import {
  AccessRequest,
  AccessReview,
  AuthorizationChange,
  AuthorizationPolicy,
  AuditLogRow,
  BreakGlassRequest,
  PermissionDefinition,
  PolicySimulationResult,
  Role,
  User,
} from "../contracts/admin.contracts";

export type {
  AccessRequest,
  AccessReview,
  AuthorizationChange,
  AuthorizationPolicy,
  AuditLogRow,
  BreakGlassRequest,
  PermissionDefinition,
  Role,
  User,
} from "../contracts/admin.contracts";

export interface CreateAccessRequest {
  subjectUserId: string;
  roleIds: string[];
  reason: string;
  expiryHours?: number;
}

export interface CreateAccessReview {
  subjectUserId: string;
  roleIds: string[];
  dueDays?: number;
}

export interface CreateBreakGlassRequest {
  subjectUserId: string;
  permissionCode: string;
  facilityId: string;
  reason: string;
  durationMinutes?: number;
  resourceType?: string;
  resourceId?: string;
}

interface AdminPageResult<T> {
  items: T[];
}

@Injectable({ providedIn: "root" })
export class AccessGovernanceApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getUsers(): Observable<User[]> {
    return this.http
      .get<AdminPageResult<User>>(`${this.baseUrl}/users`, {
        params: { page: "1", pageSize: "100" },
      })
      .pipe(map((response) => response.items));
  }

  getEffectiveAccess(
    id: string,
  ): Observable<{
    userId: string;
    isActive: boolean;
    roles: string[];
    permissions: string[];
    facilityIds: string[];
    evaluatedAt: string;
  }> {
    return this.http.get<{
      userId: string;
      isActive: boolean;
      roles: string[];
      permissions: string[];
      facilityIds: string[];
      evaluatedAt: string;
    }>(`${this.baseUrl}/users/${encodeURIComponent(id)}/effective-access`);
  }

  simulatePolicy(request: {
    userId: string;
    permissionCode: string;
    facilityId?: string;
    resourceType?: string;
    resourceId?: string;
    policyKey?: string;
    purposeOfUse?: string;
    devicePostureFresh?: boolean;
    isBreakGlass?: boolean;
    assurance?: string;
  }): Observable<PolicySimulationResult> {
    return this.http.post<PolicySimulationResult>(
      `${this.baseUrl}/policy/simulate`,
      request,
    );
  }

  getRoles(): Observable<Role[]> {
    return this.http
      .get<AdminPageResult<Role>>(`${this.baseUrl}/roles`, {
        params: { page: "1", pageSize: "100" },
      })
      .pipe(map((response) => response.items));
  }

  getPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`);
  }

  getAuditLogs(query: {
    page?: number;
    pageSize?: number;
  }): Observable<{
    items: AuditLogRow[];
    totalCount: number;
    page: number;
    pageSize: number;
  }> {
    return this.http.get<{
      items: AuditLogRow[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>(`${this.baseUrl.replace(/\/admin$/, "")}/audit-logs`, {
      params: {
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 50),
      },
    });
  }

  getBreakGlassRequests(): Observable<BreakGlassRequest[]> {
    return this.http.get<BreakGlassRequest[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass)}`,
    );
  }

  getAuthorizationChanges(): Observable<AuthorizationChange[]> {
    return this.http.get<AuthorizationChange[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.authorizationChanges)}`,
    );
  }

  getAccessRequests(): Observable<AccessRequest[]> {
    return this.http.get<AccessRequest[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests)}`,
    );
  }

  createAccessRequest(
    request: CreateAccessRequest,
  ): Observable<Pick<AccessRequest, "id" | "status" | "expiresAt">> {
    return this.http.post<Pick<AccessRequest, "id" | "status" | "expiresAt">>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests)}`,
      request,
    );
  }

  approveAccessRequest(
    id: string,
  ): Observable<
    Pick<AccessRequest, "id" | "status" | "approvedBy" | "decidedAt">
  > {
    return this.http.post<
      Pick<AccessRequest, "id" | "status" | "approvedBy" | "decidedAt">
    >(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests, id, "approve")}`,
      {},
    );
  }

  rejectAccessRequest(
    id: string,
  ): Observable<
    Pick<AccessRequest, "id" | "status" | "approvedBy" | "decidedAt">
  > {
    return this.http.post<
      Pick<AccessRequest, "id" | "status" | "approvedBy" | "decidedAt">
    >(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests, id, "reject")}`,
      {},
    );
  }

  getAccessReviews(): Observable<AccessReview[]> {
    return this.http.get<AccessReview[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews)}`,
    );
  }

  getAuthorizationPolicies(): Observable<AuthorizationPolicy[]> {
    return this.http.get<AuthorizationPolicy[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}`,
    );
  }

  createAccessReview(
    request: CreateAccessReview,
  ): Observable<Pick<AccessReview, "id" | "status" | "dueAt">> {
    return this.http.post<Pick<AccessReview, "id" | "status" | "dueAt">>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews)}`,
      request,
    );
  }

  certifyAccessReview(
    id: string,
  ): Observable<Pick<AccessReview, "id" | "status" | "decidedAt">> {
    return this.http.post<Pick<AccessReview, "id" | "status" | "decidedAt">>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews, id, "certify")}`,
      {},
    );
  }

  revokeAccessReview(
    id: string,
  ): Observable<Pick<AccessReview, "id" | "status" | "decidedAt">> {
    return this.http.post<Pick<AccessReview, "id" | "status" | "decidedAt">>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews, id, "revoke")}`,
      {},
    );
  }

  createBreakGlassRequest(
    request: CreateBreakGlassRequest,
  ): Observable<Pick<BreakGlassRequest, "id" | "status" | "expiresAt">> {
    return this.http.post<
      Pick<BreakGlassRequest, "id" | "status" | "expiresAt">
    >(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass)}`,
      request,
    );
  }

  approveBreakGlassRequest(
    id: string,
  ): Observable<Pick<BreakGlassRequest, "id" | "status" | "expiresAt">> {
    return this.http.post<
      Pick<BreakGlassRequest, "id" | "status" | "expiresAt">
    >(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass, id, "approve")}`,
      {},
    );
  }

  revokeBreakGlassRequest(id: string): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass, id, "revoke")}`,
      {},
    );
  }
}
