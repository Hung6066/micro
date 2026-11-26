import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTabsModule } from "@angular/material/tabs";
import { HttpErrorResponse } from "@angular/common/http";
import {
  AccessGovernanceApiService,
  BreakGlassRequest,
  PermissionDefinition,
  User,
} from "../../core/services/access-governance-api.service";
import { catchError, forkJoin, of, tap } from "rxjs";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import {
  InfraAuditApiService,
  InfraAuditGrantTimeline,
} from "../../core/services/infra-audit-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-break-glass-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout
      ><hh-page-header
        hhPageHeader
        [title]="'admin.breakGlass' | hhTranslate: 'Break-glass access'"
        [subtitle]="
          'admin.breakGlassSubtitle'
            | hhTranslate
              : 'Emergency elevation with short expiry and full audit.'
        "
        ><hh-action-button
          [disabled]="busy"
          (pressed)="load()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
      /></hh-page-header>
      <p class="warning">
        {{
          "admin.breakGlassWarning"
            | hhTranslate
              : "Use only for an audited emergency. The server enforces MFA, approval and expiry."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>
      <mat-card
        ><mat-card-header
          ><mat-card-title>{{
            "admin.requestBreakGlass" | hhTranslate: "Request break-glass"
          }}</mat-card-title></mat-card-header
        ><mat-card-content
          ><form [formGroup]="formGroup" class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.subject" | hhTranslate: "Subject"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.subjectUserId"
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            ><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.permission" | hhTranslate: "Permission"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.permissionCode"
                ><mat-option
                  *ngFor="let permission of permissions"
                  [value]="permission.code"
                  >{{ permission.name }} · {{ permission.code }}</mat-option
                ></mat-select
              ></mat-form-field
            >@if (
              formGroup.controls.permissionCode.value ===
              openshipConsoleWritePermission
            ) {
              <p class="hint">
                {{
                  "admin.breakGlassOpenshipConsoleWriteHint"
                    | hhTranslate
                      : "Grants write access to OpenShip consoles for at most 30 minutes; another administrator must approve."
                }}
              </p>
            }<mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.facility" | hhTranslate: "Facility"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.facilityId"
                ><mat-option
                  *ngFor="let facilityId of facilityIds"
                  [value]="facilityId"
                  >{{ facilityId }}</mat-option
                ></mat-select
              ></mat-form-field
            ><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.reason" | hhTranslate: "Reason"
              }}</mat-label
              ><textarea
                matInput
                rows="2"
                [formControl]="formGroup.controls.reason"
              ></textarea></mat-form-field
            ><hh-action-button
              [disabled]="busy || !canWrite || !canSubmit"
              (pressed)="create()"
              kind="danger"
              icon="link_off"
              [label]="
                'admin.requestBreakGlass' | hhTranslate: 'Request break-glass'
              "
            /></form></mat-card-content
      ></mat-card>
      <mat-card class="table-card"
        ><mat-card-content
          ><mat-tab-group
            [selectedIndex]="selectedTab"
            (selectedIndexChange)="selectedTab = $event"
            ><mat-tab
              [label]="
                'admin.breakGlassRequests' | hhTranslate: 'Break-glass requests'
              "
              ><hh-data-table
            [label]="'admin.breakGlassRequests' | hhTranslate"
            [columns]="columns"
            [rows]="rows"
            [loading]="state.loading"
            [empty]="!state.loading && !error && !rows.length"
            ><ng-template hhDataTableCell="actions" let-row
              ><div class="action-cell">
                <hh-action-button
                  *ngIf="row['status'] === 'pending'"
                  kind="primary"
                  mode="icon-only"
                  icon="check"
                  [label]="'admin.approve' | hhTranslate: 'Approve'"
                  [disabled]="busy || !canWrite"
                  (pressed)="approveByRow(row)"
                />
                <hh-action-button
                  *ngIf="row['status'] === 'approved'"
                  kind="danger"
                  mode="icon-only"
                  icon="link_off"
                  [label]="'admin.revoke' | hhTranslate: 'Revoke'"
                  [disabled]="busy || !canWrite"
                  (pressed)="revokeByRow(row)"
                />
                <hh-action-button
                  *ngIf="row['status'] !== 'pending'"
                  kind="secondary"
                  mode="icon-only"
                  icon="history"
                  [label]="'admin.breakGlassActivity' | hhTranslate: 'Activity'"
                  (pressed)="showActivityByRow(row)"
                /></div></ng-template></hh-data-table></mat-tab
            ><mat-tab
              [label]="'admin.breakGlassActivity' | hhTranslate: 'Activity'"
              ><div class="activity">
                @if (!activityGrant) {
                  <p class="hint">
                    {{
                      "admin.breakGlassActivityChoose"
                        | hhTranslate
                          : "Choose Activity on a grant to see what its holder did on the infrastructure while it was active."
                    }}
                  </p>
                } @else {
                  <div class="activity-head">
                    <p>
                      <strong>{{ displayUser(activityGrant.subjectUserId) }}</strong>
                      · {{ activityGrant.permissionCode }}
                      @if (timeline) {
                        · {{ timeline.from | date: "medium" }} –
                        {{ timeline.to | date: "medium" }}
                      }
                    </p>
                    <hh-action-button
                      [disabled]="activityLoading"
                      (pressed)="loadActivity()"
                      kind="secondary"
                      icon="refresh"
                      [label]="'admin.refresh' | hhTranslate: 'Refresh'"
                    />
                  </div>
                  @if (timeline?.expired) {
                    <p class="hint">
                      {{
                        "admin.breakGlassActivityExpired"
                          | hhTranslate
                            : "This grant is older than the 90-day audit retention; its activity is no longer kept."
                      }}
                    </p>
                  }
                  @if (timeline?.truncated) {
                    <p class="hint">
                      {{
                        "admin.breakGlassActivityTruncated"
                          | hhTranslate
                            : "Showing the newest events only. Open Infrastructure audit for the full list."
                      }}
                    </p>
                  }
                  <hh-data-table
                    [label]="'admin.breakGlassActivity' | hhTranslate: 'Activity'"
                    [columns]="activityColumns"
                    [rows]="activityRows"
                    [loading]="activityLoading"
                    [error]="activityError"
                    [empty]="!activityLoading && !activityError && !activityRows.length"
                    [emptyMessage]="
                      'admin.breakGlassActivityEmpty'
                        | hhTranslate
                          : 'No infrastructure activity while this grant was active.'
                    "
                  />
                }
              </div></mat-tab
            ></mat-tab-group
          ></mat-card-content
      ></mat-card>
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.form-grid{display:grid;gap:var(--space-md)}.warning{padding:var(--space-md);border:1px solid var(--color-warning);border-radius:var(--radius-card);color:var(--text-secondary)}.error{color:var(--color-danger)}.hint{margin:0;color:var(--text-secondary)}.table-card{margin-top:var(--space-lg)}.action-cell{display:flex;gap:var(--space-sm)}.activity{display:grid;gap:var(--space-md);padding-top:var(--space-md)}.activity-head{display:flex;flex-wrap:wrap;gap:var(--space-md);align-items:center;justify-content:space-between}.activity-head p{margin:0}",
  ],
})
export class BreakGlassPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly infraAudit = inject(InfraAuditApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly permissionsService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    users: User[];
    permissions: PermissionDefinition[];
    requests: BreakGlassRequest[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessGovernanceFailed",
    loadErrorFallback: "Unable to load break-glass data.",
  });
  readonly openshipConsoleWritePermission = "openship.console.write";
  users: User[] = [];
  permissions: PermissionDefinition[] = [];
  requests: BreakGlassRequest[] = [];
  busy = false;
  /** 0 = requests, 1 = activity of the selected grant. */
  selectedTab = 0;
  activityGrant: BreakGlassRequest | null = null;
  timeline: InfraAuditGrantTimeline | null = null;
  activityRows: Record<string, unknown>[] = [];
  activityLoading = false;
  activityError = "";
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  readonly facilityIds = this.permissionsService.snapshot()?.facilityIds?.length
    ? [...(this.permissionsService.snapshot()?.facilityIds ?? [])]
    : ["unassigned"];
  readonly formGroup = new FormGroup({
    subjectUserId: new FormControl("", { nonNullable: true }),
    permissionCode: new FormControl("", { nonNullable: true }),
    facilityId: new FormControl(this.facilityIds[0] ?? "", {
      nonNullable: true,
    }),
    reason: new FormControl("", { nonNullable: true }),
  });
  get canWrite(): boolean {
    return this.permissionsService.has("admin.breakglass.write");
  }
  get canSubmit(): boolean {
    const value = this.formGroup.getRawValue();
    return (
      !!value.subjectUserId &&
      !!value.permissionCode &&
      !!value.facilityId &&
      value.reason.trim().length >= 10
    );
  }
  get rows(): Record<string, unknown>[] {
    return this.requests.map((item) => ({ ...item }));
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "subjectUserId",
        label: this.i18n.t("admin.subject", "Subject"),
        computed: (row) => this.displayUser(String(row["subjectUserId"] ?? "")),
      },
      {
        key: "permissionCode",
        label: this.i18n.t("admin.permission", "Permission"),
      },
      { key: "status", label: this.i18n.t("admin.status", "Status") },
      {
        key: "expiresAt",
        label: this.i18n.t("admin.expires", "Expires"),
        format: "dateTime",
      },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
      },
    ];
  }
  get activityColumns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "timestamp", label: this.i18n.t("admin.time", "Time"), format: "dateTime" },
      { key: "source", label: this.i18n.t("admin.infraAuditSource", "Source") },
      { key: "action", label: this.i18n.t("admin.action", "Action") },
      { key: "target", label: this.i18n.t("admin.target", "Target") },
      { key: "result", label: this.i18n.t("admin.infraAuditResult", "Result") },
      { key: "sourceIp", label: this.i18n.t("admin.sourceIp", "Source IP") },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.error = "";
    this.state.load(
      forkJoin({
        users: this.api.getUsers(),
        permissions: this.api.getPermissions(),
        requests: this.api.getBreakGlassRequests(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          // The server validates break-glass codes against the code catalog, not this
          // table; keep the OpenShip console write grant selectable even where the
          // permission row has not been seeded yet.
          this.permissions = state.permissions.some(
            (permission) => permission.code === this.openshipConsoleWritePermission,
          )
            ? state.permissions
            : [
                ...state.permissions,
                {
                  code: this.openshipConsoleWritePermission,
                  name: "OpenShip console write",
                  group: "OpenShip",
                  isSystem: true,
                },
              ];
          this.requests = state.requests;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load break-glass data.",
          );
          return of({ users: [], permissions: [], requests: [] });
        }),
      ),
    );
  }
  displayUser(id: string): string {
    const user = this.users.find((x) => x.id === id);
    return user?.email || user?.userName || id;
  }
  create(): void {
    if (!this.canWrite) return;
    this.mutate(
      this.api.createBreakGlassRequest({
        ...this.formGroup.getRawValue(),
        durationMinutes: 15,
      }),
      "admin.breakGlassCreated",
      "Break-glass request created.",
      () =>
        this.formGroup.reset({
          subjectUserId: "",
          permissionCode: "",
          facilityId: this.facilityIds[0] ?? "",
          reason: "",
        }),
    );
  }
  approve(item: BreakGlassRequest): void {
    this.mutate(
      this.api.approveBreakGlassRequest(item.id),
      "admin.breakGlassApproved",
      "Break-glass request approved.",
    );
  }
  revoke(item: BreakGlassRequest): void {
    this.mutate(
      this.api.revokeBreakGlassRequest(item.id),
      "admin.breakGlassRevoked",
      "Break-glass request revoked.",
    );
  }
  /** Opens the Activity tab for one grant and loads its infrastructure timeline. */
  showActivity(item: BreakGlassRequest): void {
    this.activityGrant = item;
    this.selectedTab = 1;
    this.loadActivity();
  }
  showActivityByRow(row: Record<string, unknown>): void {
    const item = this.requests.find((request) => request.id === row["id"]);
    if (item) {
      this.showActivity(item);
    }
  }
  loadActivity(): void {
    const grant = this.activityGrant;
    if (!grant) return;
    this.activityLoading = true;
    this.activityError = "";
    this.timeline = null;
    this.activityRows = [];
    this.cdr.markForCheck();
    this.infraAudit.getGrantTimeline(grant.id).subscribe({
      next: (timeline) => {
        if (this.activityGrant?.id !== grant.id) return;
        this.timeline = timeline;
        this.activityRows = timeline.items.map((item, index) => ({
          ...item,
          id: `${item.timestamp}|${index}`,
          target: item.target ?? "",
          sourceIp: item.sourceIp ?? "",
        }));
        this.activityLoading = false;
        this.cdr.markForCheck();
      },
      error: (error: HttpErrorResponse) => {
        if (this.activityGrant?.id !== grant.id) return;
        this.activityLoading = false;
        this.activityError = this.describeActivityError(error);
        this.cdr.markForCheck();
      },
    });
  }
  approveByRow(row: Record<string, unknown>): void {
    const item = this.requests.find((request) => request.id === row["id"]);
    if (item) {
      this.approve(item);
    }
  }
  revokeByRow(row: Record<string, unknown>): void {
    const item = this.requests.find((request) => request.id === row["id"]);
    if (item) {
      this.revoke(item);
    }
  }
  private mutate(
    operation: import("rxjs").Observable<unknown>,
    key: string,
    fallback: string,
    after?: () => void,
  ): void {
    if (!this.canWrite) return;
    this.busy = true;
    this.cdr.markForCheck();
    operation.subscribe({
      next: () => {
        this.busy = false;
        after?.();
        this.toast.success(this.i18n.t(key, fallback), { duration: 3000 });
        this.cdr.markForCheck();
        this.load();
      },
      error: () => this.fail("admin.breakGlassMutationFailed", fallback),
    });
  }
  private describeActivityError(error: HttpErrorResponse): string {
    if (error.status === 403)
      return this.i18n.t(
        "admin.breakGlassActivityForbidden",
        "Only super-admins can view infrastructure activity.",
      );
    if (error.status === 404)
      return this.i18n.t("admin.breakGlassActivityNotFound", "This grant no longer exists.");
    if ((error.error as { errorCode?: string } | null)?.errorCode === "infra_audit_unavailable")
      return this.i18n.t(
        "admin.infraAuditUnavailable",
        "The audit log store is unreachable right now. Try again shortly.",
      );
    return this.i18n.t("admin.breakGlassActivityFailed", "Unable to load the grant's activity.");
  }
  private fail(key: string, fallback: string): void {
    this.error = this.i18n.t(key, fallback);
    this.toast.error(this.error, { duration: 5000 });
    this.busy = false;
    this.cdr.markForCheck();
  }
}
