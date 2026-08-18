import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { forkJoin, map, Observable, of } from "rxjs";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import { adminPageCacheKey, buildAdminPageParams } from "./admin-query.util";
import {
  IamAccessAnalyzerResult,
  IamApiAudiencesResponse,
  IamAssignment,
  IamGroup,
  IamOverview,
  IamPermissionBoundary,
  IamPermissionSet,
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
  IamTrustedIssuersResponse,
  IamUnusedPermissionsResult,
  IamWorkloadRole,
  IamWorkloadSessionResponse,
} from "../contracts/iam.contracts";
import {
  IDENTITY_WORKBENCH_ACTIONS,
  IDENTITY_WORKBENCH_RESOURCES,
  identityWorkbenchActionPath,
  identityWorkbenchPath,
} from "../contracts/identity-workbench.naming";
import type {
  AdminPageQuery,
  AdminPageResult,
  AdminSessionCenterResponse,
  AuditLogRow,
  AuthorizationPolicy,
  AuthorizationPolicyCompileResult,
  AuthorizationPolicyLintResult,
  IamAuditIntegrations,
  IamNewAccessDiffResult,
  IamRevocation,
  OidcClient,
  PermissionDefinition,
  SecuritySignalOutboxEntry,
  User,
} from "../contracts/admin.contracts";

/** IAM bounded-context API used by the control-plane workflow. */
@Injectable({ providedIn: "root" })
export class IamApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly baseUrl = environment.adminApiUrl;

  getIamScopes(): Observable<IamScope[]> {
    return this.http.get<IamScope[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes)}`,
    );
  }

  updateIamScope(
    id: string,
    request: Pick<IamScope, "key" | "displayName" | "kind"> & {
      parentId?: string | null;
    },
  ): Observable<IamScope> {
    return this.http.put<IamScope>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id)}`,
      request,
    );
  }

  getIamPermissionSets(scopeId?: string): Observable<IamPermissionSet[]> {
    const path = `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets)}`;
    return scopeId
      ? this.http.get<IamPermissionSet[]>(path, { params: { scopeId } })
      : this.http.get<IamPermissionSet[]>(path);
  }

  getIamOverview(): Observable<IamOverview> {
    return this.http.get<IamOverview>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.overview)}`,
    );
  }

  getIamGroups(scopeId?: string): Observable<IamGroup[]> {
    return this.http.get<IamGroup[]>(
      `${this.baseUrl}/iam/groups`,
      scopeId ? { params: { scopeId } } : {},
    );
  }

  getIamServicePrincipals(): Observable<
    Array<{
      id: string;
      key: string;
      displayName: string;
      principalType: string;
      audience: string;
      scopeId: string;
      isActive: boolean;
    }>
  > {
    return this.http.get<
      Array<{
        id: string;
        key: string;
        displayName: string;
        principalType: string;
        audience: string;
        scopeId: string;
        isActive: boolean;
      }>
    >(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.servicePrincipals)}`,
    );
  }

  getIamWorkloadRoles(): Observable<IamWorkloadRole[]> {
    return this.http.get<IamWorkloadRole[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles)}`,
    );
  }

  getIamWorkloadSessionCatalog(): Observable<{
    schemaVersion: string;
    evaluatedAt: string;
    sessions: Array<Record<string, unknown>>;
  }> {
    return this.http.get<{
      schemaVersion: string;
      evaluatedAt: string;
      sessions: Array<Record<string, unknown>>;
    }>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadSessions)}`,
    );
  }

  getIamApiAudiences(): Observable<IamApiAudiencesResponse> {
    return this.http.get<IamApiAudiencesResponse>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.apiAudiences)}`,
    );
  }

  getIamTrustedIssuers(): Observable<IamTrustedIssuersResponse> {
    return this.http.get<IamTrustedIssuersResponse>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.trustedIssuers)}`,
    );
  }

  getExternalIdentityProviders(): Observable<{
    providers: Array<{
      provider: string;
      displayName: string;
      icon?: string;
      protocol?: string;
      loginUrl?: string;
    }>;
  }> {
    return this.http.get<{
      providers: Array<{
        provider: string;
        displayName: string;
        icon?: string;
        protocol?: string;
        loginUrl?: string;
      }>;
    }>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.externalIdentities)}`,
    );
  }

  getIamServices(): Observable<IamServiceDefinition[]> {
    return this.http.get<IamServiceDefinition[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services)}`,
    );
  }

  getIamAssignments(
    filters: { scopeId?: string; principalId?: string } = {},
  ): Observable<IamAssignment[]> {
    return this.http.get<IamAssignment[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.assignments)}`,
      { params: filters as Record<string, string> },
    );
  }

  getAuthorizationPolicies(): Observable<AuthorizationPolicy[]> {
    return this.http.get<AuthorizationPolicy[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}`,
    );
  }

  getIamBoundaries(principalId?: string): Observable<IamPermissionBoundary[]> {
    return this.http.get<IamPermissionBoundary[]>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.boundaries)}`,
      principalId ? { params: { principalId } } : {},
    );
  }

  getIamResourcePolicies(scopeId?: string): Observable<IamResourcePolicy[]> {
    return this.http.get<IamResourcePolicy[]>(
      `${this.baseUrl}/iam/resource-policies`,
      scopeId ? { params: { scopeId } } : {},
    );
  }

  getAdminSessionCenter(): Observable<AdminSessionCenterResponse> {
    return this.http.get<AdminSessionCenterResponse>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.sessions)}`,
    );
  }

  revokeAdminSession(
    userId: string,
    sessionId: string,
    reason: string,
  ): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.users, userId)}/sessions/${encodeURIComponent(sessionId)}`,
      { params: { reason } },
    );
  }

  getIamRevocations(): Observable<{
    schemaVersion: string;
    evaluatedAt: string;
    revocations: IamRevocation[];
  }> {
    return this.http.get<{
      schemaVersion: string;
      evaluatedAt: string;
      revocations: IamRevocation[];
    }>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.revocations)}`,
    );
  }

  createIamRevocation(request: {
    principalId: string;
    principalType?: string;
    reason: string;
  }): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.revocations)}`,
      request,
    );
  }

  getAuditLogs(
    query: {
      page?: number;
      pageSize?: number;
      action?: string;
      resourceType?: string;
      userId?: string;
    } = {},
  ): Observable<{
    items: AuditLogRow[];
    totalCount: number;
    page: number;
    pageSize: number;
  }> {
    const params: Record<string, string> = {
      page: String(query.page ?? 1),
      pageSize: String(query.pageSize ?? 50),
    };
    if (query.action) params["action"] = query.action;
    if (query.resourceType) params["resourceType"] = query.resourceType;
    if (query.userId) params["userId"] = query.userId;
    return this.http.get<{
      items: AuditLogRow[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>(`${this.baseUrl.replace(/\/admin$/, "")}/audit-logs`, { params });
  }

  getIamAuditIntegrations(): Observable<IamAuditIntegrations> {
    return this.http.get<IamAuditIntegrations>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.auditIntegrations)}`,
    );
  }

  getSecuritySignalOutbox(): Observable<SecuritySignalOutboxEntry[]> {
    return this.http.get<SecuritySignalOutboxEntry[]>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/outbox`,
    );
  }

  retrySecuritySignal(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/outbox/${encodeURIComponent(id)}/retry`,
      {},
    );
  }

  analyzeIamUnusedPermissions(): Observable<IamUnusedPermissionsResult> {
    return this.http.get<IamUnusedPermissionsResult>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.unusedPermissions)}`,
    );
  }

  getIamWorkloadSessions(id: string): Observable<IamWorkloadSessionResponse> {
    return this.http.get<IamWorkloadSessionResponse>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id)}/sessions`,
    );
  }

  revokeIamWorkloadSession(id: string, sessionId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadSessions)}/${encodeURIComponent(id)}/${encodeURIComponent(sessionId)}`,
    );
  }

  revokeIamWorkloadSessions(id: string): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.revokeSessions)}`,
      {},
    );
  }

  rotateIamWorkloadCredential(id: string): Observable<{
    schemaVersion: string;
    workloadRoleId: string;
    clientId: string;
    clientAuthMethod: string;
    secret: string;
    warning: string;
  }> {
    return this.http.post<{
      schemaVersion: string;
      workloadRoleId: string;
      clientId: string;
      clientAuthMethod: string;
      secret: string;
      warning: string;
    }>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.rotateCredential)}`,
      {},
    );
  }

  revokeIamAssignment(id: string): Observable<IamAssignment> {
    return this.http.post<IamAssignment>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.assignments, id, IDENTITY_WORKBENCH_ACTIONS.revoke)}`,
      {},
    );
  }

  updateIamPermissionSet(
    id: string,
    request: Pick<IamPermissionSet, "key" | "displayName" | "scopeId"> & {
      permissions: string[];
    },
  ): Observable<IamPermissionSet> {
    return this.http.put<IamPermissionSet>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, id)}`,
      request,
    );
  }

  updateIamService(
    id: string,
    request: Pick<
      IamServiceDefinition,
      "key" | "displayName" | "permissionPrefix" | "owner"
    >,
  ): Observable<IamServiceDefinition> {
    return this.http.put<IamServiceDefinition>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services, id)}`,
      request,
    );
  }

  updateIamWorkloadRole(
    id: string,
    request: Pick<
      IamWorkloadRole,
      | "key"
      | "displayName"
      | "scopeId"
      | "audience"
      | "trustPolicyJson"
      | "maxSessionSeconds"
    > & { permissions: string[] },
  ): Observable<IamWorkloadRole> {
    return this.http.put<IamWorkloadRole>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id)}`,
      request,
    );
  }

  updateIamGroup(
    id: string,
    request: Pick<IamGroup, "key" | "displayName" | "scopeId">,
  ): Observable<IamGroup> {
    return this.http.put<IamGroup>(
      `${this.baseUrl}/iam/groups/${encodeURIComponent(id)}`,
      request,
    );
  }

  updateIamResourcePolicy(
    id: string,
    request: Pick<
      IamResourcePolicy,
      "scopeId" | "serviceKey" | "resourcePattern" | "statementsJson"
    >,
  ): Observable<IamResourcePolicy> {
    return this.http.put<IamResourcePolicy>(
      `${this.baseUrl}/iam/resource-policies/${encodeURIComponent(id)}`,
      request,
    );
  }

  createIamAssignment(
    permissionSetId: string,
    request: Pick<
      IamAssignment,
      "principalId" | "principalType" | "scopeId"
    > & { expiresAt?: string },
  ): Observable<IamAssignment> {
    return this.http.post<IamAssignment>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, permissionSetId)}/assignments`,
      request,
    );
  }

  createIamScope(
    request: Pick<IamScope, "key" | "displayName" | "kind"> & {
      parentId?: string | null;
    },
  ): Observable<IamScope> {
    return this.http.post<IamScope>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes)}`,
      request,
    );
  }

  createIamService(
    request: Pick<
      IamServiceDefinition,
      "key" | "displayName" | "permissionPrefix" | "owner"
    >,
  ): Observable<IamServiceDefinition> {
    return this.http.post<IamServiceDefinition>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services)}`,
      request,
    );
  }

  createIamPermissionSet(
    request: Pick<IamPermissionSet, "key" | "displayName" | "scopeId"> & {
      permissions: string[];
    },
  ): Observable<IamPermissionSet> {
    return this.http.post<IamPermissionSet>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets)}`,
      request,
    );
  }

  createIamWorkloadRole(
    request: Pick<
      IamWorkloadRole,
      | "key"
      | "displayName"
      | "scopeId"
      | "audience"
      | "trustPolicyJson"
      | "maxSessionSeconds"
    > & { permissions: string[] },
  ): Observable<IamWorkloadRole> {
    return this.http.post<IamWorkloadRole>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles)}`,
      request,
    );
  }

  createIamBoundary(
    request: Pick<
      IamPermissionBoundary,
      "principalId" | "principalType" | "scopeId" | "resourceConstraintsJson"
    > & { allowedPermissions: string[] },
  ): Observable<IamPermissionBoundary> {
    return this.http.post<IamPermissionBoundary>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.boundaries)}`,
      request,
    );
  }

  createIamGroup(
    request: Pick<IamGroup, "key" | "displayName" | "scopeId">,
  ): Observable<IamGroup> {
    return this.http.post<IamGroup>(`${this.baseUrl}/iam/groups`, request);
  }

  createAuthorizationPolicy(
    policy: Pick<AuthorizationPolicy, "key" | "description" | "rulesJson"> & {
      owner?: string;
    },
  ): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}`,
      policy,
    );
  }

  updateAuthorizationPolicy(
    id: string,
    policy: Pick<AuthorizationPolicy, "description" | "rulesJson"> & {
      owner?: string;
    },
  ): Observable<AuthorizationPolicy> {
    return this.http.put<AuthorizationPolicy>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies, id)}`,
      policy,
    );
  }

  createIamResourcePolicy(
    request: Pick<
      IamResourcePolicy,
      "scopeId" | "serviceKey" | "resourcePattern" | "statementsJson"
    >,
  ): Observable<IamResourcePolicy> {
    return this.http.post<IamResourcePolicy>(
      `${this.baseUrl}/iam/resource-policies`,
      request,
    );
  }

  deactivateIamScope(id: string): Observable<IamScope> {
    return this.http.post<IamScope>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`,
      {},
    );
  }

  deactivateIamService(id: string): Observable<IamServiceDefinition> {
    return this.http.post<IamServiceDefinition>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.services, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`,
      {},
    );
  }

  deactivateIamWorkloadRole(id: string): Observable<IamWorkloadRole> {
    return this.http.post<IamWorkloadRole>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`,
      {},
    );
  }

  deactivateIamGroup(id: string): Observable<IamGroup> {
    return this.http.post<IamGroup>(
      `${this.baseUrl}/iam/groups/${encodeURIComponent(id)}/deactivate`,
      {},
    );
  }

  deactivateIamBoundary(id: string): Observable<IamPermissionBoundary> {
    return this.http.post<IamPermissionBoundary>(
      `${this.baseUrl}/iam/boundaries/${encodeURIComponent(id)}/deactivate`,
      {},
    );
  }

  activateIamScope(id: string): Observable<IamScope> {
    return this.http.post<IamScope>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`,
      {},
    );
  }

  activateIamService(id: string): Observable<IamServiceDefinition> {
    return this.http.post<IamServiceDefinition>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.services, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`,
      {},
    );
  }

  activateIamWorkloadRole(id: string): Observable<IamWorkloadRole> {
    return this.http.post<IamWorkloadRole>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`,
      {},
    );
  }

  activateIamGroup(id: string): Observable<IamGroup> {
    return this.http.post<IamGroup>(
      `${this.baseUrl}/iam/groups/${encodeURIComponent(id)}/activate`,
      {},
    );
  }

  activateIamBoundary(id: string): Observable<IamPermissionBoundary> {
    return this.http.post<IamPermissionBoundary>(
      `${this.baseUrl}/iam/boundaries/${encodeURIComponent(id)}/activate`,
      {},
    );
  }

  publishIamPermissionSet(id: string): Observable<IamPermissionSet> {
    return this.http.post<IamPermissionSet>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, id, IDENTITY_WORKBENCH_ACTIONS.publish)}`,
      {},
    );
  }

  analyzeIam(): Observable<IamAccessAnalyzerResult> {
    return this.http.post<IamAccessAnalyzerResult>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.analyzer)}`,
      {},
    );
  }

  publishIamResourcePolicy(id: string): Observable<IamResourcePolicy> {
    return this.http.post<IamResourcePolicy>(
      `${this.baseUrl}/iam/resource-policies/${encodeURIComponent(id)}/publish`,
      {},
    );
  }

  lintAuthorizationPolicy(
    id: string,
  ): Observable<AuthorizationPolicyLintResult> {
    return this.http.post<AuthorizationPolicyLintResult>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, "lint")}`,
      {},
    );
  }

  publishAuthorizationPolicy(id: string): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, "publish")}`,
      {},
    );
  }

  rollbackAuthorizationPolicy(id: string): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, "rollback")}`,
      {},
    );
  }

  compileAuthorizationPolicy(
    id: string,
  ): Observable<AuthorizationPolicyCompileResult> {
    return this.http.post<AuthorizationPolicyCompileResult>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, "compile")}`,
      {},
    );
  }

  getIamEffectiveAccess(principalId: string): Observable<unknown> {
    return this.http.get(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.effectiveAccess, principalId)}`,
    );
  }

  getPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`);
  }

  simulateIamPolicy(request: {
    userId: string;
    permissionCode: string;
  }): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policySimulator)}`,
      request,
    );
  }

  analyzeIamNewAccessDiff(
    before: string[],
    after: string[],
  ): Observable<IamNewAccessDiffResult> {
    return this.http.post<IamNewAccessDiffResult>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessDiff)}`,
      { before, after },
    );
  }

  getUsers(): Observable<User[]> {
    return this.getUsersPage({ page: 1, pageSize: 100 }).pipe(
      map((response) => response.items),
    );
  }

  getClients(): Observable<OidcClient[]> {
    return this.getClientsPage({ page: 1, pageSize: 100 }).pipe(
      map((response) =>
        response.items.map((client) => ({
          ...client,
          clientType: client.clientType ?? "",
        })),
      ),
    );
  }

  private getUsersPage(
    query: AdminPageQuery,
  ): Observable<AdminPageResult<User>> {
    const params = this.pageParams(query);
    return this.requestCache.getOrLoad(adminPageCacheKey("users", params), () =>
      this.http.get<AdminPageResult<User>>(`${this.baseUrl}/users`, { params }),
    );
  }

  private getClientsPage(
    query: AdminPageQuery,
  ): Observable<AdminPageResult<OidcClient>> {
    const params = this.pageParams(query);
    return this.requestCache
      .getOrLoad(adminPageCacheKey("clients", params), () =>
        this.http.get<AdminPageResult<OidcClient & { type?: string }>>(
          `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.clients)}`,
          { params },
        ),
      )
      .pipe(
        map((response) => ({
          ...response,
          items: response.items.map((client) => ({
            ...client,
            clientType: client.clientType ?? client.type ?? "",
          })),
        })),
      );
  }

  private pageParams(query: AdminPageQuery): Record<string, string> {
    return buildAdminPageParams(query);
  }

  loadView(view: string): Observable<unknown> {
    switch (view) {
      case "overview":
        return this.getIamOverview();
      case "organizations-tenants":
      case "accounts-environments":
        return this.getIamScopes();
      case "workforce-users":
      case "effective-access":
      case "policy-simulator":
        return this.getUsers();
      case "groups":
        return this.getIamGroups();
      case "service-principals":
        return this.getIamServicePrincipals();
      case "oauth-clients":
        return this.getClients();
      case "workload-roles":
        return this.getIamWorkloadRoles();
      case "workload-sessions":
        return this.getIamWorkloadSessionCatalog();
      case "api-audiences":
        return this.getIamApiAudiences();
      case "trusted-issuers":
        return this.getIamTrustedIssuers();
      case "external-identities":
        return this.getExternalIdentityProviders();
      case "service-catalog":
        return this.getIamServices();
      case "permission-sets":
        return this.getIamPermissionSets();
      case "policies":
        return this.getAuthorizationPolicies();
      case "boundaries":
        return this.getIamBoundaries();
      case "resource-policies":
        return this.getIamResourcePolicies();
      case "active-sessions":
        return this.getAdminSessionCenter();
      case "revocations":
        return this.getIamRevocations();
      case "audit-integrations":
        return forkJoin({
          audit: this.getAuditLogs({ page: 1, pageSize: 100 }),
          integration: this.getIamAuditIntegrations(),
        });
      case "assignments":
        return forkJoin({
          assignments: this.getIamAssignments(),
          sets: this.getIamPermissionSets(),
          scopes: this.getIamScopes(),
          users: this.getUsers(),
          groups: this.getIamGroups(),
          workloadRoles: this.getIamWorkloadRoles(),
        });
      case "new-access-diff":
        return forkJoin({
          sets: this.getIamPermissionSets(),
          users: this.getUsers(),
        });
      case "analyzer":
        return forkJoin({
          users: this.getUsers(),
          sets: this.getIamPermissionSets(),
        });
      case "unused-permissions":
        return this.analyzeIamUnusedPermissions();
      default:
        return of(null);
    }
  }
}
