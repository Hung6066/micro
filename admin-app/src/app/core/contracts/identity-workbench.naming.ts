/**
 * Canonical Identity Workbench vocabulary shared by the admin UI and API
 * service. Keep the legacy `/iam` base route: changing it would break older
 * admin clients. New code must compose paths from this contract.
 */
export const IDENTITY_WORKBENCH_BASE = '/iam' as const;

export const IDENTITY_WORKBENCH_RESOURCES = {
  overview: 'overview',
  users: 'users',
  externalIdentities: 'external-identities',
  servicePrincipals: 'service-principals',
  clients: 'clients',
  scopes: 'scopes',
  services: 'services',
  permissionSets: 'permission-sets',
  assignments: 'assignments',
  workloadRoles: 'workload-roles',
  groups: 'groups',
  boundaries: 'boundaries',
  resourcePolicies: 'resource-policies',
  apiAudiences: 'api-audiences',
  trustedIssuers: 'trusted-issuers',
  policies: 'policies',
  accessRequests: 'access-requests',
  accessReviews: 'access-reviews',
  breakGlass: 'break-glass/requests',
  authorizationChanges: 'authorization-changes',
  sessions: 'sessions',
  workloadSessions: 'workload-sessions',
  revocations: 'revocations',
  analyzer: 'analyzer',
  effectiveAccess: 'analyzer/effective-access',
  policySimulator: 'analyzer/policy-simulator',
  accessDiff: 'analyzer/access-diff',
  unusedPermissions: 'analyzer/unused-permissions',
  auditIntegrations: 'audit-integrations',
  auditLogs: 'audit-logs',
} as const;

export const IDENTITY_WORKBENCH_ACTIONS = {
  create: 'create',
  update: 'update',
  delete: 'delete',
  activate: 'activate',
  deactivate: 'deactivate',
  publish: 'publish',
  revoke: 'revoke',
  revokeSessions: 'revoke-sessions',
  rotateCredential: 'rotate-credential',
  simulate: 'simulate',
  compile: 'compile',
  reconcile: 'reconcile',
  export: 'export',
} as const;

export const identityWorkbenchPath = (resource: string, id?: string): string =>
  id ? `${IDENTITY_WORKBENCH_BASE}/${resource}/${encodeURIComponent(id)}` : `${IDENTITY_WORKBENCH_BASE}/${resource}`;

export const identityWorkbenchActionPath = (resource: string, id: string, action: string): string =>
  `${identityWorkbenchPath(resource, id)}/${action}`;
