import { Component } from '@angular/core';
import { IamControlPlanePageComponent } from '../iam-control-plane/iam-control-plane-page.component';

// Route-owned components. Each declaration is statically decorated so the
// production AOT compiler never falls back to the Angular JIT compiler.

@Component({ selector: 'app-iam-overview-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="overview" />` })
export class IamOverviewPageComponent {}
@Component({ selector: 'app-iam-scopes-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="organizations-tenants" />` })
export class IamScopesPageComponent {}
@Component({ selector: 'app-iam-users-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="workforce-users" />` })
export class IamUsersPageComponent {}
@Component({ selector: 'app-iam-groups-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="groups" />` })
export class IamGroupsPageComponent {}
@Component({ selector: 'app-iam-external-identities-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="external-identities" />` })
export class IamExternalIdentitiesPageComponent {}
@Component({ selector: 'app-iam-service-principals-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="service-principals" />` })
export class IamServicePrincipalsPageComponent {}
@Component({ selector: 'app-iam-workload-roles-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="workload-roles" />` })
export class IamWorkloadRolesPageComponent {}
@Component({ selector: 'app-iam-clients-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="oauth-clients" />` })
export class IamClientsPageComponent {}
@Component({ selector: 'app-iam-api-audiences-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="api-audiences" />` })
export class IamApiAudiencesPageComponent {}
@Component({ selector: 'app-iam-trusted-issuers-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="trusted-issuers" />` })
export class IamTrustedIssuersPageComponent {}
@Component({ selector: 'app-iam-services-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="service-catalog" />` })
export class IamServicesPageComponent {}
@Component({ selector: 'app-iam-permission-sets-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="permission-sets" />` })
export class IamPermissionSetsPageComponent {}
@Component({ selector: 'app-iam-policies-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="policies" />` })
export class IamPoliciesPageComponent {}
@Component({ selector: 'app-iam-boundaries-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="boundaries" />` })
export class IamBoundariesPageComponent {}
@Component({ selector: 'app-iam-resource-policies-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="resource-policies" />` })
export class IamResourcePoliciesPageComponent {}
@Component({ selector: 'app-iam-assignments-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="assignments" />` })
export class IamAssignmentsPageComponent {}
@Component({ selector: 'app-iam-sessions-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="active-sessions" />` })
export class IamSessionsPageComponent {}
@Component({ selector: 'app-iam-workload-sessions-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="workload-sessions" />` })
export class IamWorkloadSessionsPageComponent {}
@Component({ selector: 'app-iam-revocations-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="revocations" />` })
export class IamRevocationsPageComponent {}
@Component({ selector: 'app-iam-effective-access-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="effective-access" />` })
export class IamEffectiveAccessPageComponent {}
@Component({ selector: 'app-iam-policy-simulator-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="policy-simulator" />` })
export class IamPolicySimulatorPageComponent {}
@Component({ selector: 'app-iam-access-diff-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="new-access-diff" />` })
export class IamAccessDiffPageComponent {}
@Component({ selector: 'app-iam-unused-permissions-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="unused-permissions" />` })
export class IamUnusedPermissionsPageComponent {}
@Component({ selector: 'app-iam-audit-integrations-page', standalone: true, imports: [IamControlPlanePageComponent], template: `<app-iam-control-plane-page initialView="audit-integrations" />` })
export class IamAuditIntegrationsPageComponent {}
