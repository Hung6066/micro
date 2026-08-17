/** Canonical admin IAM control-plane contracts. Keep aligned with IdentityApiRoutes.AdminIam. */
export interface IamScope { id: string; key: string; displayName: string; kind: string; parentId?: string | null; isActive: boolean; }
export interface IamOverview { schemaVersion: string; evaluatedAt: string; scopes: number; services: number; publishedPermissionSets: number; activeAssignments: number; groups: number; workloadRoles: number; pendingAccessRequests: number; pendingAccessReviews: number; pendingBreakGlass: number; auditEventsLast24Hours: number; }
export interface IamApiAudience { id: string; key: string; displayName: string; audience: string; scopeId: string; isActive: boolean; maxSessionSeconds: number; lifecycleStatus: string; }
export interface IamApiAudiencesResponse { schemaVersion: string; evaluatedAt: string; audiences: IamApiAudience[]; }
export interface IamTrustedIssuer { key: string; displayName: string; issuer: string; protocol: string; configured: boolean; active: boolean; }
export interface IamTrustedIssuersResponse { schemaVersion: string; evaluatedAt: string; issuers: IamTrustedIssuer[]; }
export interface IamServiceDefinition { id: string; key: string; displayName: string; permissionPrefix: string; owner: string; isActive: boolean; }
export interface IamPermissionSet { id: string; key: string; displayName: string; scopeId: string; permissionsJson: string; version: number; lifecycleStatus: string; publishedAt?: string; }
export interface IamAssignment { id: string; permissionSetId: string; principalId: string; principalType: string; scopeId: string; status: string; expiresAt?: string; createdAt: string; }
export interface IamWorkloadRole { id: string; key: string; displayName: string; scopeId: string; audience: string; trustPolicyJson: string; permissionsJson: string; maxSessionSeconds: number; }
export interface IamWorkloadSession { sessionId: string; clientId: string; workloadRoleId: string; issuedAt: string; expiresAt: string; }
export interface IamWorkloadSessionResponse { id: string; audience: string; sessions: IamWorkloadSession[]; }
export interface IamPermissionBoundary { id: string; principalId: string; principalType: string; scopeId: string; allowedPermissionsJson: string; resourceConstraintsJson: string; isActive: boolean; }
export interface IamResourcePolicy { id: string; scopeId: string; serviceKey: string; resourcePattern: string; statementsJson: string; lifecycleStatus: string; version: number; publishedAt?: string; }
export interface IamGroup { id: string; key: string; displayName: string; scopeId: string; isActive: boolean; }
export interface IamAccessAnalyzerResult { schemaVersion: string; analyzedAt: string; findingCount: number; findings: Array<{ resourceType: string; resourceId: string; severity: string; code: string; message: string }>; }
export interface IamNewAccessDiffResult { schemaVersion: string; added: string[]; removed: string[]; unchanged: string[]; evaluatedAt: string; }
export interface IamUnusedPermissionsResult { schemaVersion: string; unusedPermissions: string[]; count: number; evaluatedAt: string; }
