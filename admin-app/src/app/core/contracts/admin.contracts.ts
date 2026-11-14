import type {
  IamAccessAnalyzerResult,
  IamApiAudience,
  IamApiAudiencesResponse,
  IamAssignment,
  IamGroup,
  IamNewAccessDiffResult,
  IamOverview,
  IamPermissionBoundary,
  IamPermissionSet,
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
  IamServicePrincipal,
  IamTrustedIssuer,
  IamTrustedIssuersResponse,
  IamUnusedPermissionsResult,
  IamWorkloadRole,
  IamWorkloadSession,
  IamWorkloadSessionResponse,
} from "./iam.contracts";
import type {
  HisHopeBulkActionResult,
  HisHopePageQuery,
  HisHopePageResult,
  HisHopeSavedTableView,
  HisHopeTableViewRecord,
} from "@his-hope/frontend-foundation";

export type {
  IamAccessAnalyzerResult,
  IamApiAudience,
  IamApiAudiencesResponse,
  IamAssignment,
  IamGroup,
  IamNewAccessDiffResult,
  IamOverview,
  IamPermissionBoundary,
  IamPermissionSet,
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
  IamServicePrincipal,
  IamTrustedIssuer,
  IamTrustedIssuersResponse,
  IamUnusedPermissionsResult,
  IamWorkloadRole,
  IamWorkloadSession,
  IamWorkloadSessionResponse,
};

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
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
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

export interface AdminBulkActionResponse extends HisHopeBulkActionResult {}

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

export interface AdminTableView extends HisHopeTableViewRecord {}

export interface AdminSavedTableView extends HisHopeSavedTableView {}

export interface AdminTableAnalysisResult {
  resource: string;
  operation: "aggregate" | "pivot";
  groupBy: string;
  rows: Array<{ key: string; count: number }>;
  total: number;
}

export interface IdentitySetting {
  key: string;
  value: unknown;
  description?: string;
}

export interface DevicePosturePolicy {
  id: string;
  mode: "observe" | "stepup" | "deny" | string;
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
  createdAt?: string;
  publishedAt?: string;
  publishedBy?: string;
}

export interface AuthorizationChangeRequest {
  id: string;
  resourceType: string;
  resourceId: string;
  action: string;
  requestedBy: string;
  reason: string;
  status: string;
  approvedBy?: string;
  requestedAt: string;
  decidedAt?: string;
  executedAt?: string;
  expiresAt: string;
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
  status: "healthy" | "pending" | "failed" | string;
}
export interface DeliveryHealth {
  mode: string;
  ssfEnabled: boolean;
  generatedAt: string;
  deliveries: DeliveryHealthRow[];
}
export interface PolicySimulationResult {
  decision: "allow" | "deny" | string;
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
  status?: "queued" | "dry-run" | "completed" | "failed" | string;
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
  status: "active" | "expired" | "revoked" | string;
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
  audit: {
    appendOnly: boolean;
    redactionEnabled: boolean;
    lastEventAt?: string;
  };
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
  platform: "android" | "ios" | string;
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
  vault?: {
    configured: boolean;
    reachable: boolean;
    sealed: boolean;
    keyName: string;
    keyVersion?: number;
    error?: string;
  };
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
