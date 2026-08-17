import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, from, timer } from 'rxjs';
import { catchError, filter, map, switchMap, take, takeWhile, tap } from 'rxjs/operators';
import { HisHopeBulkActionRequest, HisHopePageQuery, HisHopePageResult, HisHopeRequestCacheService, HisHopeTableExportRequest } from '@his-hope/frontend-foundation';
import { environment } from '../../../environments/environment';
import { IamAccessAnalyzerResult, IamApiAudiencesResponse, IamAssignment, IamGroup, IamNewAccessDiffResult, IamOverview, IamPermissionBoundary, IamPermissionSet, IamResourcePolicy, IamScope, IamServiceDefinition, IamTrustedIssuersResponse, IamUnusedPermissionsResult, IamWorkloadRole, IamWorkloadSessionResponse } from '../contracts/iam.contracts';
import { IDENTITY_WORKBENCH_ACTIONS, IDENTITY_WORKBENCH_RESOURCES, identityWorkbenchActionPath, identityWorkbenchPath } from '../contracts/identity-workbench.naming';
export type { IamAccessAnalyzerResult, IamApiAudience, IamApiAudiencesResponse, IamAssignment, IamGroup, IamNewAccessDiffResult, IamOverview, IamPermissionBoundary, IamPermissionSet, IamResourcePolicy, IamScope, IamServiceDefinition, IamTrustedIssuer, IamTrustedIssuersResponse, IamUnusedPermissionsResult, IamWorkloadRole, IamWorkloadSession, IamWorkloadSessionResponse } from '../contracts/iam.contracts';

export interface OidcClient {
  id?: string;
  clientId: string;
  displayName: string;
  clientType: string;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  scopes?: string[];
  grantTypes?: string[];
  jwks?: string;
  concurrencyToken?: string;
}

export interface ClientSecretResponse {
  clientId: string;
  clientSecret: string;
  message: string;
  tokenEndpointAuthMethod?: string;
}

export interface ClientOnboarding {
  clientId: string;
  displayName: string;
  issuer: string;
  authorizationEndpoint: string;
  tokenEndpoint: string;
  jwksUri: string;
  grantTypes: string[];
  scopes: string[];
  tokenEndpointAuthMethod: string;
}

export interface User {
  id: string;
  userName: string;
  email: string;
  roles: string[];
  isActive: boolean;
  concurrencyToken?: string;
}

export interface Role {
  id?: string;
  name: string;
  description?: string;
  concurrencyToken?: string;
  owner?: string;
  version?: number;
  riskTier?: string;
  reviewCadenceDays?: number;
  lifecycleStatus?: string;
  publishedAt?: string;
  publishedBy?: string;
  permissions?: string[] | PermissionDefinition[];
}

export interface RoleOwnerOption {
  key: string;
  name: string;
}

export interface RoleTemplateVersion {
  id: string;
  roleId: string;
  version: number;
  name: string;
  description?: string;
  owner: string;
  riskTier: string;
  reviewCadenceDays: number;
  lifecycleStatus: string;
  permissionsJson: string;
  createdBy?: string;
  createdAt: string;
  publishedAt?: string;
  publishedBy?: string;
}

export interface Consent {
  id: string;
  subject: string;
  clientId: string;
  scopes: string[];
  created: string;
}

export interface DashboardStats {
  totalClients: number;
  totalUsers: number;
  totalRoles: number;
  totalConsents: number;
}

export type AdminPageQuery = HisHopePageQuery;

export interface AdminPageResult<T> extends HisHopePageResult<T> {}

export interface AdminBulkActionResponse {
  actionId: string;
  requested: number;
  updated: number;
}

export interface AdminJobContract {
  jobId: string;
  resource: string;
  actionId: string;
  status: string;
  processed: number;
  total: number;
  downloadUrl?: string;
  errorCode?: string;
}

export interface AdminTableView { name: string; payloadJson: string; updatedAt: string; }
export interface AdminTableAnalysisResult { resource: string; operation: 'aggregate' | 'pivot'; groupBy: string; rows: Array<{ key: string; count: number }>; total: number; }
export interface IdentitySetting { key: string; value: unknown; description?: string; }


export interface DevicePosturePolicy {
  id: string;
  mode: 'observe' | 'stepup' | 'deny' | string;
  providers: string[];
  evidenceTtlSeconds: number;
  requiredSignals: string[];
  version: string;
  updatedAt: string;
}

export interface PermissionDefinition {
  code: string;
  name: string;
  group: string;
  description?: string;
  isSystem: boolean;
  owner?: string;
  version?: number;
  riskTier?: string;
  requiredAssurance?: string;
  auditClass?: string;
  isDeprecated?: boolean;
  replacedBy?: string;
}

export interface BreakGlassRequest {
  id: string;
  subjectUserId: string;
  permissionCode: string;
  resourceType?: string;
  resourceId?: string;
  facilityId: string;
  reason: string;
  status: string;
  requestedBy: string;
  approvedBy?: string;
  requestedAt: string;
  approvedAt?: string;
  expiresAt: string;
  revokedAt?: string;
}

export interface AuthorizationChange {
  id: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  userId: string;
  details?: string;
  beforeJson?: string;
  afterJson?: string;
  timestamp: string;
  correlationId?: string;
}

export interface AccessRequest {
  id: string;
  subjectUserId: string;
  requestedBy: string;
  roleIdsJson: string;
  reason: string;
  status: string;
  approvedBy?: string;
  requestedAt: string;
  decidedAt?: string;
  expiresAt: string;
}

export interface AccessReview {
  id: string;
  subjectUserId: string;
  reviewer: string;
  roleIdsJson: string;
  status: string;
  decisionReason?: string;
  createdAt: string;
  dueAt: string;
  decidedAt?: string;
}

export interface AuthorizationPolicy {
  id: string;
  key: string;
  description: string;
  owner: string;
  version: number;
  lifecycleStatus: string;
  rulesJson: string;
  createdBy?: string;
  createdAt: string;
  publishedAt?: string;
  publishedBy?: string;
}

export interface AuthorizationPolicyLintResult {
  id: string;
  key: string;
  version: number;
  valid: boolean;
  errors: string[];
  checkedAt: string;
}

export interface AuthorizationPolicyCompileResult {
  schemaVersion: string;
  policyId: string;
  policyKey: string;
  version: number;
  valid: boolean;
  artifact?: string;
  hash?: string;
  errors: string[];
  compiledAt: string;
}

export interface AuthorizationPolicyBundle {
  schemaVersion: string;
  hash: string;
  keyId?: string;
  signature: string;
  generatedAt: string;
  policies: AuthorizationPolicy[];
}

export interface SecuritySignalOutboxEntry {
  id: string;
  eventType: string;
  createdAt: string;
  availableAt: string;
  attempts: number;
  lastError?: string;
}

export interface ProvisioningReadinessTarget {
  target: string;
  enabled: boolean;
  status: string;
  endpointHost?: string;
  tokenHost?: string;
  credentialConfigured: boolean;
}

export interface ProvisioningReadiness {
  mode: string;
  targets: ProvisioningReadinessTarget[];
}

export interface DeliveryHealthRow {
  channel: string;
  target: string;
  pending: number;
  failed: number;
  oldestAvailableAt?: string;
  status: 'healthy' | 'pending' | 'failed' | string;
}

export interface DeliveryHealth {
  mode: string;
  ssfEnabled: boolean;
  generatedAt: string;
  deliveries: DeliveryHealthRow[];
}

export interface PolicySimulationResult {
  decision: 'allow' | 'deny' | string;
  reason: string;
  userId: string;
  permissionCode: string;
  facilityId?: string;
  resourceType?: string;
  resourceId?: string;
  evaluatedAt: string;
}

export interface DevicePostureEvidence {
  userId: string;
  deviceId: string;
  provider: string;
  signals: Record<string, boolean>;
  observedAt: string;
  replayNonce?: string;
}

export interface DevicePostureEvaluation {
  decision: string;
  fresh: boolean;
  meetsRequirements: boolean;
  provider: string;
  policyVersion: string;
  expiresAt: string;
  evidenceHash: string;
}

export interface DevicePostureAssessment {
  id: string;
  userId: string;
  deviceId: string;
  provider: string;
  evidenceHashPrefix: string;
  observedAt: string;
  expiresAt: string;
  fresh: boolean;
  decision: string;
  policyVersion: string;
  correlationId?: string;
}

export interface ProvisioningJob {
  id: string;
  target: string;
  operation: string;
  resourceType: string;
  resourceId: string;
  externalId?: string;
  completedAt?: string;
  attempts: number;
  lastError?: string;
  status?: 'queued' | 'dry-run' | 'completed' | 'failed' | string;
}

export interface AuditLogRow {
  id: string;
  userId: string;
  userName?: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  outcome?: string;
  correlationId?: string;
  timestamp: string;
  details?: string;
  beforeJson?: string;
  afterJson?: string;
}

export interface MtlsBinding {
  id: string;
  userId: string;
  thumbprint: string;
  subject?: string;
  notAfter: string;
  revokedAt?: string;
  status: 'active' | 'expired' | 'revoked' | string;
}

export interface RadiusEapTlsStatus {
  enabled: boolean;
  trustedCaConfigured: boolean;
  trustedCaReachable: boolean;
  sharedSecretManagedBy: string;
}

export interface SecuritySignalStatus {
  enabled: boolean;
  subscriptionCount: number;
  subscriptions: Array<{ host: string; audience?: string }>;
  pending: number;
  failed: number;
  lastDelivery?: string;
}

export interface IamRevocation {
  id: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  userId?: string;
  reason?: string;
  occurredAt: string;
  correlationId?: string;
}

export interface IamAuditIntegrations {
  schemaVersion: string;
  evaluatedAt: string;
  audit: { appendOnly: boolean; redactionEnabled: boolean; lastEventAt?: string };
  ssf: { enabled: boolean; pending: number };
}

export interface PlatformResource {
  name: string;
  displayName?: string;
  type?: string;
  status?: string;
  healthStatus?: string;
  engine?: string;
  sizeMb?: number;
  connections?: number;
  healthChecks?: Array<{ name: string; status: string; output?: string }>;
}

export interface MobileDeviceRegistration {
  id: string;
  userId: string;
  platform: 'android' | 'ios' | string;
  registeredAt: string;
  lastSeenAt: string;
  revokedAt?: string;
  active: boolean;
}

export interface MobileDeliverySummary {
  since: string;
  queued: number;
  processed: number;
  pending: number;
  sent: number;
  failed: number;
  byPlatform: Array<{ platform: string; sent: number; failed: number }>;
  lastFailure?: { platform: string; errorCode?: string; createdAt: string };
}

export interface AdminSession {
  userId?: string;
  email?: string;
  id: string;
  deviceInfo?: string;
  issuedAt?: string;
  expiresAt?: string;
  active: boolean;
}

export interface AdminSessionCenterResponse {
  schemaVersion: string;
  evaluatedAt: string;
  sessions: AdminSession[];
}

export interface BulkImportResult {
  created: number;
  skipped: number;
  failed: number;
  errors?: Array<{ row?: number; email?: string; error: string }>;
}

export interface BulkImportPreview {
  total: number;
  valid: number;
  invalid: number;
  rows: Array<{ row: number; valid: boolean; user: Record<string, unknown> }>;
}

export interface DatabaseContinuityStatus {
  enabled: boolean;
  schedulerEnabled?: boolean;
  backupIntervalHours?: number;
  restoreDrillIntervalHours?: number;
  maxAttempts?: number;
  provider: string;
  storageProvider?: string;
  storageUri?: string;
  pitrEnabled: boolean;
  encryption: { provider: string; configured: boolean };
  retentionDays: number;
  keepLastBackupsPerDatabase?: number;
  targetRpoMinutes: number;
  targetRtoMinutes: number;
  lastSuccessfulBackupAt?: string;
  lastSuccessfulRestoreDrillAt?: string;
  executorConfigured: boolean;
  ready: boolean;
  configurationErrors: string[];
  alerts?: string[];
  vault?: { configured: boolean; reachable: boolean; sealed: boolean; keyName: string; keyVersion?: number; error?: string };
  latestJob?: DatabaseContinuityJob;
}

export interface DatabaseContinuityAuditEntry {
  jobId: string;
  operation: string;
  targetEnvironment: string;
  status: string;
  actorSubject: string;
  correlationId: string;
  createdAt: string;
  updatedAt: string;
  errorCode?: string;
  resultJson?: string;
}

export interface DatabaseContinuityJob {
  jobId: string;
  operation: string;
  status: string;
  errorCode?: string;
  correlationId?: string;
  createdAt: string;
  updatedAt: string;
  result?: string;
}

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly baseUrl = environment.adminApiUrl;
  private readonly dashboardBffUrl = environment.dashboardBffUrl;

  getClients(): Observable<OidcClient[]> {
    return this.getClientsPage({ page: 1, pageSize: 100 }).pipe(
      map(response => response.items.map(client => ({
        ...client,
        clientType: client.clientType ?? '',
      }))),
    );
  }

  getClientsPage(query: AdminPageQuery): Observable<AdminPageResult<OidcClient>> {
    const params = this.pageParams(query);
    return this.requestCache.getOrLoad(`admin:clients:${params.toString()}`, () => this.http.get<AdminPageResult<OidcClient & { type?: string }>>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.clients)}`, { params })).pipe(
      map(response => ({
        items: response.items.map(client => ({ ...client, clientType: client.clientType ?? client.type ?? '' })),
        totalCount: response.totalCount,
        page: response.page,
        pageSize: response.pageSize,
        totalPages: response.totalPages,
        hasNextPage: response.hasNextPage,
        hasPreviousPage: response.hasPreviousPage,
      })),
    );
  }

  getClient(id: string): Observable<OidcClient> {
    return this.http.get<OidcClient>(`${this.baseUrl}/clients/${id}`);
  }

  createClient(client: Partial<OidcClient>): Observable<ClientSecretResponse> {
    return this.http.post<ClientSecretResponse>(`${this.baseUrl}/clients`, client).pipe(tap(() => this.requestCache.invalidate('admin:clients:')));
  }

  updateClient(id: string, client: Partial<OidcClient>): Observable<OidcClient> {
    return this.http.put<OidcClient>(`${this.baseUrl}/clients/${id}`, client).pipe(tap(() => this.requestCache.invalidate('admin:clients:')));
  }

  deleteClient(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/clients/${id}`).pipe(tap(() => this.requestCache.invalidate('admin:clients:')));
  }

  rotateClientSecret(id: string): Observable<ClientSecretResponse> {
    return this.http.post<ClientSecretResponse>(`${this.baseUrl}/clients/${id}/rotate-secret`, {});
  }

  getClientOnboarding(id: string): Observable<ClientOnboarding> {
    return this.http.get<ClientOnboarding>(`${this.baseUrl}/clients/${id}/onboarding`);
  }

  getUsers(): Observable<User[]> {
    return this.getUsersPage({ page: 1, pageSize: 100 }).pipe(map(response => response.items));
  }

  getUsersPage(query: AdminPageQuery): Observable<AdminPageResult<User>> {
    const requestParams = this.pageParams(query);
    return this.requestCache.getOrLoad(`admin:users:${requestParams.toString()}`, () => this.http.get<AdminPageResult<User>>(`${this.baseUrl}/users`, { params: requestParams }));
  }

  bulkUsers(request: HisHopeBulkActionRequest): Observable<AdminBulkActionResponse> {
    return this.bulkTable('users', request);
  }

  bulkTable(resource: 'users' | 'roles' | 'clients', request: HisHopeBulkActionRequest): Observable<AdminBulkActionResponse> {
    return this.http.post<AdminBulkActionResponse>(`${this.baseUrl}/tables/${resource}/bulk`, {
      actionId: request.actionId,
      rowKeys: request.rowKeys,
      query: request.query,
      selection: request.selection,
    }).pipe(tap(() => this.requestCache.invalidate(`admin:${resource}:`)));
  }

  exportTable(resource: 'users' | 'roles' | 'clients' | 'audit', request: HisHopeTableExportRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/tables/${resource}/export`, {
      format: request.format,
      columns: request.columns,
      rowKeys: request.rowKeys,
      query: request.query,
      async: request.async ?? false,
      maskSensitive: request.maskSensitive ?? true,
    }, { observe: 'response', responseType: 'blob' }).pipe(
      switchMap(response => {
        const contentType = response.headers.get('content-type') ?? '';
        if (!contentType.includes('json')) return of(response.body as Blob);
        return from((response.body as Blob).text()).pipe(
          map(text => JSON.parse(text) as AdminJobContract),
          switchMap(job => this.streamAdminJob(job.jobId).pipe(catchError(() => this.waitForAdminJob(job.jobId)))),
        );
      }),
    );
  }

  getTableViews(resource: 'users' | 'roles' | 'clients'): Observable<AdminTableView[]> {
    return this.http.get<AdminTableView[]>(`${this.baseUrl}/tables/${resource}/views`);
  }

  saveTableView(resource: 'users' | 'roles' | 'clients', name: string, payload: unknown): Observable<AdminTableView> {
    return this.http.put<AdminTableView>(`${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`, { payloadJson: JSON.stringify(payload) });
  }

  deleteTableView(resource: 'users' | 'roles' | 'clients', name: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`);
  }

  analyzeTable(resource: 'users' | 'roles' | 'clients', operation: 'aggregate' | 'pivot' | 'formula', groupBy: string, formulaId?: string, detailLimit = 0): Observable<AdminTableAnalysisResult> {
    return this.http.post<AdminTableAnalysisResult>(`${this.baseUrl}/tables/${resource}/analysis`, { operation, groupBy, metric: 'count', formulaId, detailLimit });
  }

  previewUserImport(file: File): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/users/bulk/preview`, file, { headers: { 'Content-Type': file.type || 'text/csv' } });
  }

  createUser(request: { username: string; email: string; password: string; firstName: string; lastName: string; middleName?: string; licenseNumber?: string; specialty?: string; phoneNumber?: string; role?: string }): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/users`, request).pipe(tap(() => this.requestCache.invalidate('admin:users:')));
  }

  updateUser(id: string, request: { firstName?: string; lastName?: string; email?: string; phoneNumber?: string; role?: string; isActive?: boolean; concurrencyToken?: string }): Observable<User> {
    return this.http.put<User>(`${this.baseUrl}/users/${encodeURIComponent(id)}`, request).pipe(tap(() => this.requestCache.invalidate('admin:users:')));
  }

  deactivateUser(id: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/users/${encodeURIComponent(id)}/deactivate`, {}).pipe(tap(() => this.requestCache.invalidate('admin:users:'))); }
  activateUser(id: string): Observable<void> { return this.http.put<void>(`${this.baseUrl}/users/${encodeURIComponent(id)}/activate`, {}).pipe(tap(() => this.requestCache.invalidate('admin:users:'))); }

  getExternalIdentityProviders(): Observable<{ providers: Array<{ provider: string; displayName: string; icon?: string; protocol?: string; loginUrl?: string }> }> {
    return this.http.get<{ providers: Array<{ provider: string; displayName: string; icon?: string; protocol?: string; loginUrl?: string }> }>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.externalIdentities)}`);
  }

  getIamServicePrincipals(): Observable<Array<{ id: string; key: string; displayName: string; principalType: string; audience: string; scopeId: string; isActive: boolean }>> {
    return this.http.get<Array<{ id: string; key: string; displayName: string; principalType: string; audience: string; scopeId: string; isActive: boolean }>>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.servicePrincipals)}`);
  }

  importUsers(file: File): Observable<BulkImportResult> {
    return this.http.post<BulkImportResult>(`${this.baseUrl}/users/bulk/file`, file, { headers: { 'Content-Type': file.type || 'text/csv' } }).pipe(
      tap(() => this.requestCache.invalidate('admin:users:')),
    );
  }

  private waitForAdminJob(jobId: string): Observable<Blob> {
    return timer(0, 1000).pipe(
      switchMap(() => this.http.get<AdminJobContract>(`${this.baseUrl}/tables/jobs/${jobId}`)),
      takeWhile(job => job.status !== 'Completed' && job.status !== 'Failed' && job.status !== 'Cancelled', true),
      filter(job => job.status === 'Completed'),
      take(1),
      switchMap(() => this.http.get(`${this.baseUrl}/tables/jobs/${jobId}/download`, { responseType: 'blob' })),
    );
  }

  private streamAdminJob(jobId: string): Observable<Blob> {
    return new Observable<AdminJobContract>(subscriber => {
      const source = new EventSource(`${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/events`, { withCredentials: true });
      source.addEventListener('job', event => {
        try {
          const job = JSON.parse((event as MessageEvent).data) as AdminJobContract;
          subscriber.next(job);
          if (job.status === 'Completed' || job.status === 'Failed' || job.status === 'Cancelled') { source.close(); subscriber.complete(); }
        } catch (error) { source.close(); subscriber.error(error); }
      });
      source.onerror = () => { source.close(); subscriber.error(new Error('SSE job stream unavailable')); };
      return () => source.close();
    }).pipe(
      filter(job => job.status === 'Completed'),
      take(1),
      switchMap(() => this.http.get(`${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/download`, { responseType: 'blob' })),
    );
  }

  getRoles(): Observable<Role[]> {
    return this.getRolesPage({ page: 1, pageSize: 100 }).pipe(map(response => response.items));
  }

  getRolesPage(query: AdminPageQuery): Observable<AdminPageResult<Role>> {
    const requestParams = this.pageParams(query);
    return this.requestCache.getOrLoad(`admin:roles:${requestParams.toString()}`, () => this.http.get<AdminPageResult<Role>>(`${this.baseUrl}/roles`, { params: requestParams }));
  }

  createRole(role: Partial<Role>): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/roles`, role).pipe(tap(() => this.requestCache.invalidate('admin:roles:')));
  }

  getIamScopes(): Observable<IamScope[]> { return this.http.get<IamScope[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes)}`); }
  getIamOverview(): Observable<IamOverview> { return this.http.get<IamOverview>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.overview)}`); }
  createIamScope(request: Pick<IamScope, 'key' | 'displayName' | 'kind'> & { parentId?: string | null }): Observable<IamScope> { return this.http.post<IamScope>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes)}`, request); }
  updateIamScope(id: string, request: Pick<IamScope, 'key' | 'displayName' | 'kind'> & { parentId?: string | null }): Observable<IamScope> { return this.http.put<IamScope>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id)}`, request); }
  deactivateIamScope(id: string): Observable<IamScope> { return this.http.post<IamScope>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`, {}); }
  activateIamScope(id: string): Observable<IamScope> { return this.http.post<IamScope>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.scopes, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`, {}); }
  getIamServices(): Observable<IamServiceDefinition[]> { return this.http.get<IamServiceDefinition[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services)}`); }
  getIamApiAudiences(): Observable<IamApiAudiencesResponse> { return this.http.get<IamApiAudiencesResponse>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.apiAudiences)}`); }
  getIamTrustedIssuers(): Observable<IamTrustedIssuersResponse> { return this.http.get<IamTrustedIssuersResponse>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.trustedIssuers)}`); }
  createIamService(request: Pick<IamServiceDefinition, 'key' | 'displayName' | 'permissionPrefix' | 'owner'>): Observable<IamServiceDefinition> { return this.http.post<IamServiceDefinition>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services)}`, request); }
  updateIamService(id: string, request: Pick<IamServiceDefinition, 'key' | 'displayName' | 'permissionPrefix' | 'owner'>): Observable<IamServiceDefinition> { return this.http.put<IamServiceDefinition>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.services, id)}`, request); }
  deactivateIamService(id: string): Observable<IamServiceDefinition> { return this.http.post<IamServiceDefinition>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.services, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`, {}); }
  activateIamService(id: string): Observable<IamServiceDefinition> { return this.http.post<IamServiceDefinition>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.services, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`, {}); }
  getIamPermissionSets(scopeId?: string): Observable<IamPermissionSet[]> { const path = `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets)}`; return scopeId ? this.http.get<IamPermissionSet[]>(path, { params: { scopeId } }) : this.http.get<IamPermissionSet[]>(path); }
  getIamAssignments(filters: { scopeId?: string; principalId?: string } = {}): Observable<IamAssignment[]> { return this.http.get<IamAssignment[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.assignments)}`, { params: filters as Record<string, string> }); }
  createIamAssignment(permissionSetId: string, request: Pick<IamAssignment, 'principalId' | 'principalType' | 'scopeId'> & { expiresAt?: string }): Observable<IamAssignment> { return this.http.post<IamAssignment>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, permissionSetId)}/assignments`, request); }
  revokeIamAssignment(id: string): Observable<IamAssignment> { return this.http.post<IamAssignment>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.assignments, id, IDENTITY_WORKBENCH_ACTIONS.revoke)}`, {}); }
  createIamPermissionSet(request: Pick<IamPermissionSet, 'key' | 'displayName' | 'scopeId'> & { permissions: string[] }): Observable<IamPermissionSet> { return this.http.post<IamPermissionSet>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets)}`, request); }
  updateIamPermissionSet(id: string, request: Pick<IamPermissionSet, 'key' | 'displayName' | 'scopeId'> & { permissions: string[] }): Observable<IamPermissionSet> { return this.http.put<IamPermissionSet>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, id)}`, request); }
  publishIamPermissionSet(id: string): Observable<IamPermissionSet> { return this.http.post<IamPermissionSet>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.permissionSets, id, IDENTITY_WORKBENCH_ACTIONS.publish)}`, {}); }
  getIamWorkloadRoles(): Observable<IamWorkloadRole[]> { return this.http.get<IamWorkloadRole[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles)}`); }
  createIamWorkloadRole(request: Pick<IamWorkloadRole, 'key' | 'displayName' | 'scopeId' | 'audience' | 'trustPolicyJson' | 'maxSessionSeconds'> & { permissions: string[] }): Observable<IamWorkloadRole> { return this.http.post<IamWorkloadRole>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles)}`, request); }
  updateIamWorkloadRole(id: string, request: Pick<IamWorkloadRole, 'key' | 'displayName' | 'scopeId' | 'audience' | 'trustPolicyJson' | 'maxSessionSeconds'> & { permissions: string[] }): Observable<IamWorkloadRole> { return this.http.put<IamWorkloadRole>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id)}`, request); }
  deactivateIamWorkloadRole(id: string): Observable<IamWorkloadRole> { return this.http.post<IamWorkloadRole>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.deactivate)}`, {}); }
  activateIamWorkloadRole(id: string): Observable<IamWorkloadRole> { return this.http.post<IamWorkloadRole>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.activate)}`, {}); }
  getIamWorkloadSessions(id: string): Observable<IamWorkloadSessionResponse> { return this.http.get<IamWorkloadSessionResponse>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id)}/sessions`); }
  getIamWorkloadSessionCatalog(): Observable<{ schemaVersion: string; evaluatedAt: string; sessions: Array<Record<string, unknown>> }> { return this.http.get<{ schemaVersion: string; evaluatedAt: string; sessions: Array<Record<string, unknown>> }>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadSessions)}`); }
  revokeIamWorkloadSession(id: string, sessionId: string): Observable<void> { return this.http.delete<void>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.workloadSessions)}/${encodeURIComponent(id)}/${encodeURIComponent(sessionId)}`); }
  revokeIamWorkloadSessions(id: string): Observable<unknown> { return this.http.post(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.revokeSessions)}`, {}); }
  rotateIamWorkloadCredential(id: string): Observable<{ schemaVersion: string; workloadRoleId: string; clientId: string; clientAuthMethod: string; secret: string; warning: string }> { return this.http.post<{ schemaVersion: string; workloadRoleId: string; clientId: string; clientAuthMethod: string; secret: string; warning: string }>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.workloadRoles, id, IDENTITY_WORKBENCH_ACTIONS.rotateCredential)}`, {}); }
  getIamBoundaries(principalId?: string): Observable<IamPermissionBoundary[]> { return this.http.get<IamPermissionBoundary[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.boundaries)}`, principalId ? { params: { principalId } } : {}); }
  createIamBoundary(request: Pick<IamPermissionBoundary, 'principalId' | 'principalType' | 'scopeId' | 'resourceConstraintsJson'> & { allowedPermissions: string[] }): Observable<IamPermissionBoundary> { return this.http.post<IamPermissionBoundary>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.boundaries)}`, request); }
  deactivateIamBoundary(id: string): Observable<IamPermissionBoundary> { return this.http.post<IamPermissionBoundary>(`${this.baseUrl}/iam/boundaries/${encodeURIComponent(id)}/deactivate`, {}); }
  activateIamBoundary(id: string): Observable<IamPermissionBoundary> { return this.http.post<IamPermissionBoundary>(`${this.baseUrl}/iam/boundaries/${encodeURIComponent(id)}/activate`, {}); }
  getIamResourcePolicies(scopeId?: string): Observable<IamResourcePolicy[]> { return this.http.get<IamResourcePolicy[]>(`${this.baseUrl}/iam/resource-policies`, scopeId ? { params: { scopeId } } : {}); }
  createIamResourcePolicy(request: Pick<IamResourcePolicy, 'scopeId' | 'serviceKey' | 'resourcePattern' | 'statementsJson'>): Observable<IamResourcePolicy> { return this.http.post<IamResourcePolicy>(`${this.baseUrl}/iam/resource-policies`, request); }
  updateIamResourcePolicy(id: string, request: Pick<IamResourcePolicy, 'scopeId' | 'serviceKey' | 'resourcePattern' | 'statementsJson'>): Observable<IamResourcePolicy> { return this.http.put<IamResourcePolicy>(`${this.baseUrl}/iam/resource-policies/${encodeURIComponent(id)}`, request); }
  publishIamResourcePolicy(id: string): Observable<IamResourcePolicy> { return this.http.post<IamResourcePolicy>(`${this.baseUrl}/iam/resource-policies/${encodeURIComponent(id)}/publish`, {}); }
  getIamGroups(scopeId?: string): Observable<IamGroup[]> { return this.http.get<IamGroup[]>(`${this.baseUrl}/iam/groups`, scopeId ? { params: { scopeId } } : {}); }
  createIamGroup(request: Pick<IamGroup, 'key' | 'displayName' | 'scopeId'>): Observable<IamGroup> { return this.http.post<IamGroup>(`${this.baseUrl}/iam/groups`, request); }
  updateIamGroup(id: string, request: Pick<IamGroup, 'key' | 'displayName' | 'scopeId'>): Observable<IamGroup> { return this.http.put<IamGroup>(`${this.baseUrl}/iam/groups/${encodeURIComponent(id)}`, request); }
  deactivateIamGroup(id: string): Observable<IamGroup> { return this.http.post<IamGroup>(`${this.baseUrl}/iam/groups/${encodeURIComponent(id)}/deactivate`, {}); }
  activateIamGroup(id: string): Observable<IamGroup> { return this.http.post<IamGroup>(`${this.baseUrl}/iam/groups/${encodeURIComponent(id)}/activate`, {}); }
  analyzeIam(): Observable<IamAccessAnalyzerResult> { return this.http.post<IamAccessAnalyzerResult>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.analyzer)}`, {}); }
  getIamEffectiveAccess(principalId: string): Observable<unknown> { return this.http.get(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.effectiveAccess, principalId)}`); }
  simulateIamPolicy(request: { userId: string; permissionCode: string }): Observable<unknown> { return this.http.post(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policySimulator)}`, request); }
  analyzeIamNewAccessDiff(before: string[], after: string[]): Observable<IamNewAccessDiffResult> { return this.http.post<IamNewAccessDiffResult>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessDiff)}`, { before, after }); }
  analyzeIamUnusedPermissions(): Observable<IamUnusedPermissionsResult> { return this.http.get<IamUnusedPermissionsResult>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.unusedPermissions)}`); }
  getIamRevocations(): Observable<{ schemaVersion: string; evaluatedAt: string; revocations: IamRevocation[] }> { return this.http.get<{ schemaVersion: string; evaluatedAt: string; revocations: IamRevocation[] }>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.revocations)}`); }
  createIamRevocation(request: { principalId: string; principalType?: string; reason: string }): Observable<unknown> { return this.http.post(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.revocations)}`, request); }
  getIamAuditIntegrations(): Observable<IamAuditIntegrations> { return this.http.get<IamAuditIntegrations>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.auditIntegrations)}`); }

  getRole(id: string): Observable<Role & { permissions?: PermissionDefinition[] }> {
    return this.http.get<Role & { permissions?: PermissionDefinition[] }>(`${this.baseUrl}/roles/${encodeURIComponent(id)}`);
  }

  updateRole(id: string, role: Partial<Role> & { permissions?: string[] }): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/roles/${encodeURIComponent(id)}`, role).pipe(tap(() => this.requestCache.invalidate('admin:roles:')));
  }

  getRoleTemplateVersions(id: string): Observable<RoleTemplateVersion[]> {
    return this.http.get<RoleTemplateVersion[]>(`${this.baseUrl}/roles/${encodeURIComponent(id)}/versions`);
  }

  publishRole(id: string): Observable<{ id: string; authorizationVersion: number; lifecycleStatus: string; publishedAt: string }> {
    return this.http.post<{ id: string; authorizationVersion: number; lifecycleStatus: string; publishedAt: string }>(`${this.baseUrl}/roles/${encodeURIComponent(id)}/publish`, {});
  }

  rollbackRole(id: string): Observable<{ id: string; authorizationVersion: number; lifecycleStatus: string; restoredFromVersion: number }> {
    return this.http.post<{ id: string; authorizationVersion: number; lifecycleStatus: string; restoredFromVersion: number }>(`${this.baseUrl}/roles/${encodeURIComponent(id)}/rollback`, {});
  }

  getPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`);
  }

  getRoleOwners(): Observable<RoleOwnerOption[]> {
    return this.http.get<RoleOwnerOption[]>(`${this.baseUrl}/role-owners`);
  }

  getBreakGlassRequests(): Observable<BreakGlassRequest[]> {
    return this.http.get<BreakGlassRequest[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass)}`);
  }

  getAuthorizationChanges(): Observable<AuthorizationChange[]> {
    return this.http.get<AuthorizationChange[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.authorizationChanges)}`);
  }

  getAccessRequests(): Observable<AccessRequest[]> {
    return this.http.get<AccessRequest[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests)}`);
  }

  createAccessRequest(request: { subjectUserId: string; roleIds: string[]; reason: string; expiryHours?: number }): Observable<Pick<AccessRequest, 'id' | 'status' | 'expiresAt'>> {
    return this.http.post<Pick<AccessRequest, 'id' | 'status' | 'expiresAt'>>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests)}`, request);
  }

  approveAccessRequest(id: string): Observable<Pick<AccessRequest, 'id' | 'status' | 'approvedBy' | 'decidedAt'>> {
    return this.http.post<Pick<AccessRequest, 'id' | 'status' | 'approvedBy' | 'decidedAt'>>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests, id, 'approve')}`, {});
  }

  rejectAccessRequest(id: string): Observable<Pick<AccessRequest, 'id' | 'status' | 'approvedBy' | 'decidedAt'>> {
    return this.http.post<Pick<AccessRequest, 'id' | 'status' | 'approvedBy' | 'decidedAt'>>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessRequests, id, 'reject')}`, {});
  }

  getAccessReviews(): Observable<AccessReview[]> {
    return this.http.get<AccessReview[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews)}`);
  }

  createAccessReview(request: { subjectUserId: string; roleIds: string[]; dueDays?: number }): Observable<Pick<AccessReview, 'id' | 'status' | 'dueAt'>> {
    return this.http.post<Pick<AccessReview, 'id' | 'status' | 'dueAt'>>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews)}`, request);
  }

  certifyAccessReview(id: string): Observable<Pick<AccessReview, 'id' | 'status' | 'decidedAt'>> {
    return this.http.post<Pick<AccessReview, 'id' | 'status' | 'decidedAt'>>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews, id, 'certify')}`, {});
  }

  revokeAccessReview(id: string): Observable<Pick<AccessReview, 'id' | 'status' | 'decidedAt'>> {
    return this.http.post<Pick<AccessReview, 'id' | 'status' | 'decidedAt'>>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.accessReviews, id, 'revoke')}`, {});
  }

  getAuthorizationPolicies(): Observable<AuthorizationPolicy[]> {
    return this.http.get<AuthorizationPolicy[]>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}`);
  }

  getAuthorizationPolicyBundle(): Observable<AuthorizationPolicyBundle> {
    return this.http.get<AuthorizationPolicyBundle>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}/bundle`);
  }

  createAuthorizationPolicy(policy: Pick<AuthorizationPolicy, 'key' | 'description' | 'rulesJson'> & { owner?: string }): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies)}`, policy);
  }

  updateAuthorizationPolicy(id: string, policy: Pick<AuthorizationPolicy, 'description' | 'rulesJson'> & { owner?: string }): Observable<AuthorizationPolicy> {
    return this.http.put<AuthorizationPolicy>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.policies, id)}`, policy);
  }

  lintAuthorizationPolicy(id: string): Observable<AuthorizationPolicyLintResult> {
    return this.http.post<AuthorizationPolicyLintResult>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, 'lint')}`, {});
  }

  compileAuthorizationPolicy(id: string): Observable<AuthorizationPolicyCompileResult> {
    return this.http.post<AuthorizationPolicyCompileResult>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, 'compile')}`, {});
  }

  publishAuthorizationPolicy(id: string): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, 'publish')}`, {});
  }

  rollbackAuthorizationPolicy(id: string): Observable<AuthorizationPolicy> {
    return this.http.post<AuthorizationPolicy>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.policies, id, 'rollback')}`, {});
  }

  createBreakGlassRequest(request: { subjectUserId: string; permissionCode: string; facilityId: string; reason: string; durationMinutes?: number; resourceType?: string; resourceId?: string }): Observable<Pick<BreakGlassRequest, 'id' | 'status' | 'expiresAt'>> {
    return this.http.post<Pick<BreakGlassRequest, 'id' | 'status' | 'expiresAt'>>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass)}`, request);
  }

  revokeBreakGlassRequest(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass, id, 'revoke')}`, {});
  }

  approveBreakGlassRequest(id: string): Observable<Pick<BreakGlassRequest, 'id' | 'status' | 'expiresAt'>> {
    return this.http.post<Pick<BreakGlassRequest, 'id' | 'status' | 'expiresAt'>>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.breakGlass, id, 'approve')}`, {});
  }

  simulatePolicy(request: { userId: string; permissionCode: string; facilityId?: string; resourceType?: string; resourceId?: string; policyKey?: string; purposeOfUse?: string; devicePostureFresh?: boolean; isBreakGlass?: boolean; assurance?: string }): Observable<PolicySimulationResult> {
    return this.http.post<PolicySimulationResult>(`${this.baseUrl}/policy/simulate`, request);
  }

  exportAuditCsv(): Observable<Blob> {
    return this.exportTable('audit', { format: 'csv', columns: ['timestamp', 'action', 'resourceType', 'resourceId', 'outcome', 'correlationId'], rowKeys: [], query: { page: 1, pageSize: 10000 }, async: false, maskSensitive: true });
  }

  getConsents(): Observable<Consent[]> {
    return this.http.get<AdminPageResult<Consent>>(`${this.baseUrl}/consents`, { params: this.pageParams({ page: 1, pageSize: 100 }) }).pipe(
      map(response => response.items),
    );
  }

  getConsentsPage(query: AdminPageQuery): Observable<AdminPageResult<Consent>> {
    return this.http.get<AdminPageResult<Consent>>(`${this.baseUrl}/consents`, { params: this.pageParams(query) });
  }

  getMyPermissions(): Observable<{ userId?: string; userName?: string; roles: string[]; permissions: string[]; scopes?: string[]; facilityIds?: string[]; authzVersion?: string }> {
    return this.http.get<{ userId?: string; userName?: string; roles: string[]; permissions: string[]; scopes?: string[]; facilityIds?: string[]; authzVersion?: string }>(`${this.baseUrl}/me/permissions`);
  }

  getIdentitySettings(): Observable<IdentitySetting[]> {
    return this.http.get<IdentitySetting[]>(`${this.baseUrl}/settings`);
  }

  saveIdentitySettings(settings: Array<{ key: string; value: unknown }>): Observable<IdentitySetting[]> {
    // SystemSetting.Value is persisted as text. Normalize booleans and numbers
    // before serialization so ASP.NET can bind BulkUpdateSettingItem.Value.
    const payload = settings.map(setting => ({
      key: setting.key,
      value: String(setting.value ?? ''),
    }));
    return this.http.put<IdentitySetting[]>(`${this.baseUrl}/settings/bulk`, { settings: payload });
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.baseUrl}/dashboard`);
  }

  getUser(id: string): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/users/${encodeURIComponent(id)}`);
  }

  getEffectiveAccess(id: string): Observable<{ userId: string; isActive: boolean; roles: string[]; permissions: string[]; facilityIds: string[]; evaluatedAt: string }> {
    return this.http.get<{ userId: string; isActive: boolean; roles: string[]; permissions: string[]; facilityIds: string[]; evaluatedAt: string }>(`${this.baseUrl}/users/${encodeURIComponent(id)}/effective-access`);
  }

  getAdminSessions(userId: string): Observable<{ userId: string; sessions: AdminSession[] }> {
    return this.http.get<{ userId: string; sessions: AdminSession[] }>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.users, userId)}/sessions`);
  }
  getAdminSessionCenter(): Observable<AdminSessionCenterResponse> {
    return this.http.get<AdminSessionCenterResponse>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.sessions)}`);
  }

  revokeAdminSession(userId: string, sessionId: string, reason: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.users, userId)}/sessions/${encodeURIComponent(sessionId)}`, { params: { reason } });
  }

  revokeAllAdminSessions(userId: string, reason: string): Observable<{ userId: string; revokedSessions: number }> {
    return this.http.post<{ userId: string; revokedSessions: number }>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.users, userId, 'sessions/revoke-all')}`, { reason });
  }

  resetAdminCredentials(userId: string, request: { resetMfa: boolean; revokePasskeys: boolean; reason: string }): Observable<{ userId: string; removedMfa: number; removedPasskeys: number; tokensRevoked: boolean }> {
    return this.http.post<{ userId: string; removedMfa: number; removedPasskeys: number; tokensRevoked: boolean }>(`${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.users, userId, 'credentials/reset')}`, request);
  }

  assignUserRoles(id: string, roleIds: string[]): Observable<User> {
    return this.http.put<User>(`${this.baseUrl}/users/${encodeURIComponent(id)}/roles`, { roleIds }).pipe(tap(() => this.requestCache.invalidate('admin:users:')));
  }

  getDevicePosturePolicy(): Observable<DevicePosturePolicy> {
    return this.http.get<DevicePosturePolicy>(`${this.baseUrl}/device-posture/policy`);
  }

  updateDevicePosturePolicy(policy: Pick<DevicePosturePolicy, 'mode' | 'providers' | 'evidenceTtlSeconds' | 'requiredSignals'>): Observable<DevicePosturePolicy> {
    return this.http.put<DevicePosturePolicy>(`${this.baseUrl}/device-posture/policy`, policy);
  }

  rollbackDevicePosturePolicy(): Observable<DevicePosturePolicy> {
    return this.http.post<DevicePosturePolicy>(`${this.baseUrl}/device-posture/policy/rollback`, {});
  }

  previewDevicePosture(evidence: DevicePostureEvidence): Observable<DevicePostureEvaluation> {
    return this.http.post<DevicePostureEvaluation>(`${this.baseUrl}/device-posture/preview`, evidence);
  }

  getDevicePostureAssessments(): Observable<DevicePostureAssessment[]> {
    return this.http.get<DevicePostureAssessment[]>(`${this.baseUrl}/device-posture/assessments`);
  }

  queueProvisioning(request: { target: string; operation: string; resourceType: string; resourceId: string; payload: unknown; externalId?: string }): Observable<ProvisioningJob> {
    return this.http.post<ProvisioningJob>(`${this.baseUrl}/provisioning/queue`, request);
  }

  getProvisioningJob(id: string): Observable<ProvisioningJob> {
    return this.http.get<ProvisioningJob>(`${this.baseUrl}/provisioning/jobs/${encodeURIComponent(id)}`);
  }

  retryProvisioningJob(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.baseUrl}/provisioning/jobs/${encodeURIComponent(id)}/retry`, {});
  }

  reconcileProvisioning(target: 'scim' | 'entra' | 'google-workspace'): Observable<{ target: string; queued: number; sourceCount: number }> {
    return this.http.post<{ target: string; queued: number; sourceCount: number }>(`${this.baseUrl}/provisioning/reconcile/${encodeURIComponent(target)}`, {});
  }

  getProvisioningJobs(): Observable<ProvisioningJob[]> {
    return this.http.get<ProvisioningJob[]>(`${this.baseUrl}/provisioning/jobs`);
  }

  getProvisioningReadiness(): Observable<ProvisioningReadiness> {
    return this.http.get<ProvisioningReadiness>(`${this.baseUrl}/provisioning/readiness`);
  }

  getDeliveryHealth(): Observable<DeliveryHealth> {
    return this.http.get<DeliveryHealth>(`${this.baseUrl}/provisioning/delivery-health`);
  }

  getMtlsBindings(): Observable<MtlsBinding[]> {
    return this.http.get<MtlsBinding[]>(`${this.baseUrl}/mtls/bindings`);
  }

  revokeMtlsBinding(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/mtls/bindings/${encodeURIComponent(id)}`);
  }

  getRadiusEapTlsStatus(): Observable<RadiusEapTlsStatus> {
    return this.http.get<RadiusEapTlsStatus>(`${this.baseUrl.replace(/\/admin$/, '')}/admin/radius/eap-tls/status`);
  }

  getSecuritySignalStatus(): Observable<SecuritySignalStatus> {
    return this.http.get<SecuritySignalStatus>(`${this.baseUrl.replace(/\/admin$/, '')}/admin/security-signals/status`);
  }

  getSecuritySignalOutbox(): Observable<SecuritySignalOutboxEntry[]> {
    return this.http.get<SecuritySignalOutboxEntry[]>(`${this.baseUrl.replace(/\/admin$/, '')}/admin/security-signals/outbox`);
  }

  retrySecuritySignal(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.baseUrl.replace(/\/admin$/, '')}/admin/security-signals/outbox/${encodeURIComponent(id)}/retry`, {});
  }

  getAuditLogs(query: { page?: number; pageSize?: number; action?: string; resourceType?: string; userId?: string } = {}): Observable<{ items: AuditLogRow[]; totalCount: number; page: number; pageSize: number }> {
    const params: Record<string, string> = { page: String(query.page ?? 1), pageSize: String(query.pageSize ?? 50) };
    if (query.action) params['action'] = query.action;
    if (query.resourceType) params['resourceType'] = query.resourceType;
    if (query.userId) params['userId'] = query.userId;
    return this.http.get<{ items: AuditLogRow[]; totalCount: number; page: number; pageSize: number }>(`${this.baseUrl.replace(/\/admin$/, '')}/audit-logs`, { params });
  }

  getMobileDevices(query: AdminPageQuery = { page: 1, pageSize: 50 }): Observable<{ items: MobileDeviceRegistration[]; page: number; pageSize: number; total: number }> {
    return this.http.get<{ items: MobileDeviceRegistration[]; page: number; pageSize: number; total: number }>(`${this.baseUrl}/mobile/devices`, { params: this.pageParams(query) });
  }

  revokeMobileDevice(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/mobile/devices/${encodeURIComponent(id)}/revoke`, {});
  }

  getMobileDeliverySummary(hours = 24): Observable<MobileDeliverySummary> {
    return this.http.get<MobileDeliverySummary>(`${this.baseUrl}/push/delivery-summary`, { params: { hours } });
  }

  getPlatformResources(): Observable<PlatformResource[]> {
    return this.requestCache.getOrLoad('platform:resources', () =>
      this.http.get<PlatformResource[]>(`${this.dashboardBffUrl}/api/resources`),
    ).pipe(
      map(resources => resources.filter(resource =>
        resource.type === 'database' || resource.engine || resource.name.toLowerCase().includes('db'),
      )),
    );
  }

  getDatabaseContinuity(): Observable<DatabaseContinuityStatus> {
    return this.http.get<DatabaseContinuityStatus>(`${this.baseUrl}/database-continuity/status`);
  }

  getDatabaseContinuityAudit(page = 1, pageSize = 20): Observable<DatabaseContinuityAuditEntry[]> {
    return this.http.get<DatabaseContinuityAuditEntry[]>(`${this.baseUrl}/database-continuity/audit?page=${page}&pageSize=${pageSize}`);
  }

  requestDatabaseBackup(): Observable<DatabaseContinuityJob> {
    return this.http.post<DatabaseContinuityJob>(`${this.baseUrl}/database-continuity/backups`, {});
  }

  requestRestoreDrill(): Observable<DatabaseContinuityJob> {
    return this.http.post<DatabaseContinuityJob>(`${this.baseUrl}/database-continuity/restore-drills`, {});
  }

  getDatabaseContinuityJob(jobId: string): Observable<DatabaseContinuityJob> {
    return this.http.get<DatabaseContinuityJob>(`${this.baseUrl}/database-continuity/jobs/${encodeURIComponent(jobId)}`);
  }

  private pageParams(query: AdminPageQuery): Record<string, string> {
    const params: Record<string, string> = { page: String(query.page), pageSize: String(query.pageSize) };
    if (query.search?.trim()) params['search'] = query.search.trim();
    if (query.cursor) params['cursor'] = query.cursor;
    if (query.sort) {
      const sort = Array.isArray(query.sort) ? query.sort : [query.sort];
      params['sort'] = sort.map(term => `${term.key}:${term.direction}`).join(',');
    }
    for (const [key, value] of Object.entries(query.filters ?? {})) {
      if (value !== null && value !== undefined && value !== '') params[key] = String(value);
    }
    return params;
  }
}
