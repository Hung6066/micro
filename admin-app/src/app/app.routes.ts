import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";
import { adminReadGuard } from "./core/guards/admin-read.guard";
import { capabilityPermissionGuard } from "./core/guards/capability-permission.guard";

const protectedRoute = [authGuard, adminReadGuard] as const;

const iamWorkbenchRoutes: Routes = [
  {
    path: "iam/overview",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-overview/iam-overview-page.component").then(
        (m) => m.IamOverviewPageComponent,
      ),
  },
  {
    path: "iam/scopes",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-scopes/iam-scopes-page.component").then(
        (m) => m.IamScopesPageComponent,
      ),
  },
  {
    path: "iam/users",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/users/users-page.component").then(
        (m) => m.UsersPageComponent,
      ),
  },
  {
    path: "iam/groups",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/groups/groups-page.component").then(
        (m) => m.GroupsPageComponent,
      ),
  },
  {
    path: "iam/external-identities",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-applications/iam-external-identities-page.component").then(
        (m) => m.IamExternalIdentitiesPageComponent,
      ),
  },
  {
    path: "iam/service-principals",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/service-principals/service-principals-page.component").then(
        (m) => m.ServicePrincipalsPageComponent,
      ),
  },
  {
    path: "iam/workload-roles",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/workload-roles/workload-roles-page.component").then(
        (m) => m.WorkloadRolesPageComponent,
      ),
  },
  {
    path: "iam/clients",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/clients/clients-page.component").then(
        (m) => m.ClientsPageComponent,
      ),
  },
  {
    path: "iam/api-audiences",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-applications/iam-api-audiences-page.component").then(
        (m) => m.IamApiAudiencesPageComponent,
      ),
  },
  {
    path: "iam/trusted-issuers",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-applications/iam-trusted-issuers-page.component").then(
        (m) => m.IamTrustedIssuersPageComponent,
      ),
  },
  {
    path: "iam/services",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-services/iam-services-page.component").then(
        (m) => m.IamServicesPageComponent,
      ),
  },
  {
    path: "iam/permission-sets",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/permission-sets/permission-sets-page.component").then(
        (m) => m.PermissionSetsPageComponent,
      ),
  },
  {
    path: "iam/policies",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/policies/policies-page.component").then(
        (m) => m.PoliciesPageComponent,
      ),
  },
  {
    path: "iam/boundaries",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/boundaries/boundaries-page.component").then(
        (m) => m.BoundariesPageComponent,
      ),
  },
  {
    path: "iam/resource-policies",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/resource-policies/resource-policies-page.component").then(
        (m) => m.ResourcePoliciesPageComponent,
      ),
  },
  {
    path: "iam/assignments",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/assignments/assignments-page.component").then(
        (m) => m.AssignmentsPageComponent,
      ),
  },
  {
    path: "iam/sessions",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-sessions/iam-sessions-page.component").then(
        (m) => m.IamSessionsPageComponent,
      ),
  },
  {
    path: "iam/workload-sessions",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-operations/iam-operations-pages.component").then(
        (m) => m.IamWorkloadSessionsPageComponent,
      ),
  },
  {
    path: "iam/revocations",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-sessions/iam-revocations-page.component").then(
        (m) => m.IamRevocationsPageComponent,
      ),
  },
  {
    path: "iam/analyzer/effective-access",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-analyzer/iam-effective-access-page.component").then(
        (m) => m.IamEffectiveAccessPageComponent,
      ),
  },
  {
    path: "iam/analyzer/policy-simulator",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-analyzer/iam-policy-simulator-page.component").then(
        (m) => m.IamPolicySimulatorPageComponent,
      ),
  },
  {
    path: "iam/analyzer/access-diff",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-analyzer/iam-access-diff-page.component").then(
        (m) => m.IamAccessDiffPageComponent,
      ),
  },
  {
    path: "iam/analyzer/unused-permissions",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-sessions/iam-unused-permissions-page.component").then(
        (m) => m.IamUnusedPermissionsPageComponent,
      ),
  },
  {
    path: "iam/audit-integrations",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/iam-operations/iam-operations-pages.component").then(
        (m) => m.IamAuditIntegrationsPageComponent,
      ),
  },
  {
    path: "iam/access-requests",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/access-governance/access-requests-page.component").then(
        (m) => m.AccessRequestsPageComponent,
      ),
  },
  {
    path: "iam/access-reviews",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/access-governance/access-reviews-page.component").then(
        (m) => m.AccessReviewsPageComponent,
      ),
  },
  {
    path: "iam/jit-access",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/access-governance/jit-access-page.component").then(
        (m) => m.JitAccessPageComponent,
      ),
  },
  {
    path: "iam/break-glass",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/access-governance/break-glass-page.component").then(
        (m) => m.BreakGlassPageComponent,
      ),
  },
];

export const routes: Routes = [
  ...iamWorkbenchRoutes,
  { path: "", redirectTo: "/dashboard", pathMatch: "full" },
  {
    path: "clients",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/clients/clients-page.component").then(
        (m) => m.ClientsPageComponent,
      ),
  },
  {
    path: "users",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/users/users-page.component").then(
        (m) => m.UsersPageComponent,
      ),
  },
  {
    path: "roles",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/roles/roles-page.component").then(
        (m) => m.RolesPageComponent,
      ),
  },
  {
    path: "consents",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/consents/consents-page.component").then(
        (m) => m.ConsentsPageComponent,
      ),
  },
  {
    path: "security-providers",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/security-providers/security-providers-page.component").then(
        (m) => m.SecurityProvidersPageComponent,
      ),
  },
  {
    path: "dashboard",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/dashboard/dashboard-page.component").then(
        (m) => m.DashboardPageComponent,
      ),
  },
  {
    path: "access-management",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/access-management/access-management-page.component").then(
        (m) => m.AccessManagementPageComponent,
      ),
  },
  {
    path: "access-governance",
    canActivate: [...protectedRoute],
    redirectTo: "/iam/access-requests",
    pathMatch: "full",
  },
  // Compatibility bookmark only. Canonical IAM navigation is component-per-resource above.
  {
    path: "iam-control-plane",
    canActivate: [...protectedRoute],
    redirectTo: "/iam/overview",
    pathMatch: "full",
  },
  {
    path: "identity-capabilities",
    canActivate: [...protectedRoute, capabilityPermissionGuard],
    loadComponent: () =>
      import("./features/identity-capabilities/identity-capabilities-page.component").then(
        (m) => m.IdentityCapabilitiesPageComponent,
      ),
  },
  {
    path: "identity-operations",
    canActivate: [...protectedRoute, capabilityPermissionGuard],
    loadComponent: () =>
      import("./features/identity-operations/identity-operations-page.component").then(
        (m) => m.IdentityOperationsPageComponent,
      ),
  },
  {
    path: "security/identity",
    canActivate: [...protectedRoute, capabilityPermissionGuard],
    loadComponent: () =>
      import("./features/identity-capabilities/identity-capabilities-page.component").then(
        (m) => m.IdentityCapabilitiesPageComponent,
      ),
  },
  {
    path: "forbidden",
    loadComponent: () =>
      import("./features/forbidden/forbidden-page.component").then(
        (m) => m.ForbiddenPageComponent,
      ),
  },
  {
    path: "database-platform",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/database-platform/database-platform-page.component").then(
        (m) => m.DatabasePlatformPageComponent,
      ),
  },
  {
    path: "mobile-operations",
    canActivate: [...protectedRoute],
    loadComponent: () =>
      import("./features/mobile-operations/mobile-operations-page.component").then(
        (m) => m.MobileOperationsPageComponent,
      ),
  },
  {
    path: "auth/login",
    loadComponent: () =>
      import("./features/auth/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "auth/callback",
    loadComponent: () =>
      import("./features/auth/callback.component").then(
        (m) => m.CallbackComponent,
      ),
  },
  {
    path: "auth/silent-refresh",
    loadComponent: () =>
      import("./features/auth/silent-refresh.component").then(
        (m) => m.SilentRefreshComponent,
      ),
  },
  { path: "**", redirectTo: "/clients" },
];
