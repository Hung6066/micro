import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { RouterModule } from "@angular/router";
import {
  AccessRequest,
  AccessReview,
  AuthorizationChange,
  AuthorizationPolicy,
  BreakGlassRequest,
  PermissionDefinition,
  PolicySimulationResult,
  Role,
  User,
} from "../../core/contracts/admin.contracts";
import { AccessGovernanceApiService } from "../../core/services/access-governance-api.service";
import { catchError, forkJoin, of, tap, timeout } from "rxjs";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeChipsComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-access-management-page",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    HisHopeChipsComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.accessManagement' | hhTranslate: 'Access management'"
        [subtitle]="
          'admin.accessManagementSubtitle'
            | hhTranslate: 'Fine-grained RBAC administration'
        "
      />
      @if (loading) {
        <mat-spinner
          diameter="32"
          [attr.aria-label]="
            'admin.loadingAccessManagement'
              | hhTranslate: 'Loading access management'
          "
        ></mat-spinner>
      }
      @if (error) {
        <div class="hh-state hh-state--error" role="alert">{{ error }}</div>
      }
      <section
        class="access-grid"
        [attr.aria-label]="
          'admin.fineGrainedRbac'
            | hhTranslate: 'Fine-grained RBAC capabilities'
        "
      >
        <mat-card>
          <mat-card-header
            ><mat-card-title>{{
              "admin.permissionCatalog" | hhTranslate: "Permission catalog"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ permissions.length }}
              {{ "admin.permissions" | hhTranslate: "permissions" }}
            </p>
            <hh-chips
              [formControl]="permissionGroupsControl"
              [readonly]="true"
              [label]="
                'admin.permissionGroups' | hhTranslate: 'Permission groups'
              "
            />
            <div class="catalog-meta">
              <strong>{{
                "admin.permissionGovernance"
                  | hhTranslate: "Governance metadata"
              }}</strong
              ><span
                >{{ highRiskPermissionCount }}
                {{ "admin.permissionRisk" | hhTranslate: "high-risk" }}</span
              ><span>{{ catalogVersionLabel }}</span>
            </div>
            @if (permissions[0]; as permission) {
              <p class="muted">
                {{ "admin.permissionOwner" | hhTranslate: "Owner" }}:
                {{ permission.owner || "unknown" }} ·
                {{ "admin.permissionAssurance" | hhTranslate: "Assurance" }}:
                {{ permission.requiredAssurance || "standard" }} ·
                {{ "admin.permissionAuditClass" | hhTranslate: "Audit class" }}:
                {{ permission.auditClass || "authorization" }}
              </p>
            }
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-header
            ><mat-card-title>{{
              "admin.roleGovernance" | hhTranslate: "Role governance"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ roles.length }} {{ "admin.roles" | hhTranslate: "roles" }}
            </p>
            <p class="muted">
              {{ systemRoleCount }} system · {{ customRoleCount }} custom
            </p>
            <p class="muted">
              {{ pendingAccessRequestCount }}
              {{
                "admin.pendingAccessRequests"
                  | hhTranslate: "pending access requests"
              }}
            </p></mat-card-content
          >
          <mat-card-actions
            ><a mat-button routerLink="/roles">{{
              "admin.openRoles" | hhTranslate: "Open roles"
            }}</a></mat-card-actions
          >
        </mat-card>
        <mat-card>
          <mat-card-header
            ><mat-card-title>{{
              "admin.effectiveAccess" | hhTranslate: "Effective access"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ users.length }} {{ "admin.users" | hhTranslate: "users" }}
            </p>
            <p class="muted">
              {{ facilityCount }}
              {{
                "admin.facilities"
                  | hhTranslate: "facilities in current snapshot"
              }}
            </p></mat-card-content
          >
          <mat-card-actions
            ><a mat-button routerLink="/users">{{
              "admin.openUsers" | hhTranslate: "Open users"
            }}</a></mat-card-actions
          >
        </mat-card>
        <mat-card>
          <mat-card-header
            ><mat-card-title>{{
              "admin.auditAccessReview" | hhTranslate: "Audit & access review"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ auditCount }}
              {{
                "admin.recentAuditEvents" | hhTranslate: "recent audit events"
              }}
            </p>
            <p class="muted">
              {{ authorizationChanges.length }}
              {{
                "admin.authorizationChanges"
                  | hhTranslate: "authorization changes"
              }}
            </p>
            <p class="muted">
              {{ pendingAccessReviewCount }}
              {{
                "admin.pendingAccessReviews"
                  | hhTranslate: "pending access reviews"
              }}
            </p>
            <p class="muted">
              {{
                "admin.auditServerEnforced"
                  | hhTranslate: "Server-filtered and redacted"
              }}
            </p></mat-card-content
          >
          <mat-card-actions
            ><a mat-button routerLink="/identity-capabilities">{{
              "admin.auditLogs" | hhTranslate: "Open audit logs"
            }}</a></mat-card-actions
          >
        </mat-card>
        <mat-card class="capability-card">
          <mat-card-header
            ><mat-card-title>{{
              "admin.breakGlass" | hhTranslate: "Break-glass access"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ breakGlassRequests.length }}
              {{ "admin.breakGlassRequests" | hhTranslate: "requests" }}
            </p>
            <p class="muted">
              {{
                "admin.breakGlassRequiresReview"
                  | hhTranslate
                    : "Every request is persisted, time-bounded and audited."
              }}
            </p>
            <button
              *ngIf="can('admin.breakglass.write')"
              mat-button
              type="button"
              (click)="createBreakGlassRequest()"
              [disabled]="!users.length || !permissions.length"
            >
              {{ "admin.requestBreakGlass" | hhTranslate: "Create request" }}
            </button></mat-card-content
          >
        </mat-card>
        <mat-card class="capability-card">
          <mat-card-header
            ><mat-card-title>{{
              "admin.abacPolicyCatalog" | hhTranslate: "ABAC policy catalog"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{ authorizationPolicies.length }}
              {{
                "admin.authorizationPolicies"
                  | hhTranslate: "versioned policies"
              }}
            </p>
            <p class="muted">
              {{ publishedPolicyCount }}
              {{ "admin.publishedPolicies" | hhTranslate: "published" }} ·
              {{ draftPolicyCount }}
              {{ "admin.draftPolicies" | hhTranslate: "draft" }}
            </p>
            <p class="muted">
              {{
                "admin.abacPolicyCatalogText"
                  | hhTranslate
                    : "Policies are validated server-side and never grant access from the UI."
              }}
            </p></mat-card-content
          >
        </mat-card>
        <mat-card class="capability-card">
          <mat-card-header
            ><mat-card-title>{{
              "admin.policySimulator" | hhTranslate: "Policy simulator"
            }}</mat-card-title></mat-card-header
          >
          <mat-card-content
            ><p>
              {{
                "admin.policySimulatorText"
                  | hhTranslate
                    : "Server-side identity scope preview; it never grants access."
              }}
            </p>
            <button
              *ngIf="can('admin.policy.simulate')"
              mat-button
              type="button"
              (click)="simulatePolicy()"
              [disabled]="!users.length || !permissions.length"
            >
              {{
                "admin.simulatePolicy" | hhTranslate: "Simulate current user"
              }}
            </button>
            @if (simulation) {
              <p
                class="simulation"
                [class.simulation--deny]="simulation.decision === 'deny'"
              >
                <strong>{{ simulation.decision }}</strong> ·
                {{ simulation.reason }}
              </p>
            }
          </mat-card-content>
        </mat-card>
      </section>
      <section
        class="implementation-note"
        [attr.aria-label]="
          'admin.authorizationBoundary' | hhTranslate: 'Implementation boundary'
        "
      >
        <strong>{{
          "admin.authorizationBoundary" | hhTranslate: "Authorization boundary"
        }}</strong>
        <span>{{
          "admin.authorizationBoundaryText"
            | hhTranslate
              : "This page is an administrative view. Identity Service and domain services remain the enforcement point for permission, resource and facility decisions."
        }}</span>
      </section>
    </hh-page-layout>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .access-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
        gap: 16px;
      }
      mat-card {
        min-height: 170px;
      }
      .chips {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
      }
      .muted {
        color: var(--text-muted);
      }
      .catalog-meta {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-2);
        margin-top: var(--space-3);
        color: var(--text-muted);
        font-size: var(--font-size-caption);
      }
      .capability-card--planned {
        border-style: dashed;
      }
      .implementation-note {
        display: flex;
        gap: var(--space-2);
        margin-top: var(--space-5);
        padding: var(--space-3);
        border-radius: var(--radius-card);
        background: var(--surface-subtle);
      }
      mat-spinner {
        margin: 8px auto 20px;
      }
      @media (max-width: 640px) {
        .implementation-note {
          flex-direction: column;
        }
      }
    `,
  ],
})
export class AccessManagementPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    permissions: PermissionDefinition[];
    roles: Role[];
    users: User[];
    audit: { totalCount: number };
    breakGlassRequests: BreakGlassRequest[];
    authorizationChanges: AuthorizationChange[];
    accessRequests: AccessRequest[];
    accessReviews: AccessReview[];
    authorizationPolicies: AuthorizationPolicy[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessManagementFailed",
    loadErrorFallback: "Unable to load access management data.",
  });
  permissions: PermissionDefinition[] = [];
  roles: Role[] = [];
  users: User[] = [];
  auditCount = 0;
  breakGlassRequests: BreakGlassRequest[] = [];
  authorizationChanges: AuthorizationChange[] = [];
  accessRequests: AccessRequest[] = [];
  accessReviews: AccessReview[] = [];
  authorizationPolicies: AuthorizationPolicy[] = [];
  simulation: PolicySimulationResult | null = null;
  facilityCount = 0;
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }

  permissionGroups: string[] = [];
  readonly permissionGroupsControl = new FormControl<string[]>([], {
    nonNullable: true,
  });
  get highRiskPermissionCount(): number {
    return this.permissions.filter(
      (permission) => permission.riskTier === "high",
    ).length;
  }
  get pendingAccessRequestCount(): number {
    return this.accessRequests.filter((request) => request.status === "pending")
      .length;
  }
  get pendingAccessReviewCount(): number {
    return this.accessReviews.filter((review) => review.status === "pending")
      .length;
  }
  get catalogVersionLabel(): string {
    const version = this.permissions[0]?.version ?? 1;
    return `${this.i18n.t("admin.permissionVersion", "Catalog version")} ${version}`;
  }
  get systemRoleCount(): number {
    return this.roles.filter(
      (role) => (role as Role & { isSystem?: boolean }).isSystem,
    ).length;
  }
  get customRoleCount(): number {
    return this.roles.length - this.systemRoleCount;
  }
  get publishedPolicyCount(): number {
    return this.authorizationPolicies.filter(
      (policy) => policy.lifecycleStatus === "published",
    ).length;
  }
  get draftPolicyCount(): number {
    return this.authorizationPolicies.filter(
      (policy) => policy.lifecycleStatus === "draft",
    ).length;
  }
  can(permission: string): boolean {
    return this.permissionService.has(permission);
  }

  ngOnInit(): void {
    this.state.load(
      forkJoin({
        permissions: this.api.getPermissions(),
        roles: this.api.getRoles(),
        users: this.api.getUsers(),
        audit: this.api.getAuditLogs({ page: 1, pageSize: 1 }),
        breakGlassRequests: this.api.getBreakGlassRequests(),
        authorizationChanges: this.api.getAuthorizationChanges(),
        accessRequests: this.api.getAccessRequests(),
        accessReviews: this.api.getAccessReviews(),
        authorizationPolicies: this.api.getAuthorizationPolicies(),
      }).pipe(
        timeout(8000),
        tap((state) => {
          this.permissions = state.permissions;
          this.permissionGroups = [
            ...new Set(state.permissions.map((permission) => permission.group)),
          ].sort();
          this.permissionGroupsControl.setValue(this.permissionGroups);
          this.roles = state.roles;
          this.users = state.users;
          this.auditCount = state.audit.totalCount;
          this.breakGlassRequests = state.breakGlassRequests;
          this.authorizationChanges = state.authorizationChanges;
          this.accessRequests = state.accessRequests;
          this.accessReviews = state.accessReviews;
          this.authorizationPolicies = state.authorizationPolicies;
          const firstUser = state.users[0];
          if (firstUser)
            this.api.getEffectiveAccess(firstUser.id).subscribe({
              next: (access) =>
                (this.facilityCount = access.facilityIds.length),
              error: () =>
                (this.facilityCount =
                  this.permissionService.snapshot()?.facilityIds?.length ?? 0),
            });
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessManagementFailed",
            "Unable to load access management data.",
          );
          return of({
            permissions: [],
            roles: [],
            users: [],
            audit: { totalCount: 0 },
            breakGlassRequests: [],
            authorizationChanges: [],
            accessRequests: [],
            accessReviews: [],
            authorizationPolicies: [],
          });
        }),
      ),
    );
  }

  createBreakGlassRequest(): void {
    if (!this.can("admin.breakglass.write")) return;
    const user = this.users[0];
    const permission = this.permissions[0];
    const facilityId =
      this.permissionService.snapshot()?.facilityIds?.[0] ?? "unassigned";
    if (!user || !permission) return;
    this.api
      .createBreakGlassRequest({
        subjectUserId: user.id,
        permissionCode: permission.code,
        facilityId,
        reason: this.i18n.t(
          "admin.breakGlassPilotReason",
          "Emergency access requested from Access Management pilot.",
        ),
        durationMinutes: 15,
      })
      .subscribe({
        next: () =>
          this.api
            .getBreakGlassRequests()
            .subscribe((requests) => (this.breakGlassRequests = requests)),
        error: () =>
          (this.error = this.i18n.t(
            "admin.createBreakGlassFailed",
            "Unable to create break-glass request.",
          )),
      });
  }

  simulatePolicy(): void {
    if (!this.can("admin.policy.simulate")) return;
    const user = this.users[0];
    const permission = this.permissions[0];
    if (!user || !permission) return;
    this.api
      .simulatePolicy({
        userId: user.id,
        permissionCode: permission.code,
        facilityId: this.permissionService.snapshot()?.facilityIds?.[0],
      })
      .subscribe({
        next: (result) => (this.simulation = result),
        error: () =>
          (this.error = this.i18n.t(
            "admin.simulatePolicyFailed",
            "Unable to simulate policy.",
          )),
      });
  }
}
