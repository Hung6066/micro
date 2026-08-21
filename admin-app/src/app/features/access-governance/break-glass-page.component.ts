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
            ><mat-form-field appearance="outline"
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
        ><mat-card-header
          ><mat-card-title>{{
            "admin.breakGlassRequests" | hhTranslate: "Break-glass requests"
          }}</mat-card-title></mat-card-header
        ><mat-card-content
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
                /></div></ng-template></hh-data-table></mat-card-content
      ></mat-card>
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.form-grid{display:grid;gap:var(--space-md)}.warning{padding:var(--space-md);border:1px solid var(--color-warning);border-radius:var(--radius-card);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-lg)}.action-cell{display:flex;gap:var(--space-sm)}",
  ],
})
export class BreakGlassPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
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
  users: User[] = [];
  permissions: PermissionDefinition[] = [];
  requests: BreakGlassRequest[] = [];
  busy = false;
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
  ngOnInit(): void {
    this.load();
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
          this.permissions = state.permissions;
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
  private fail(key: string, fallback: string): void {
    this.error = this.i18n.t(key, fallback);
    this.toast.error(this.error, { duration: 5000 });
    this.busy = false;
    this.cdr.markForCheck();
  }
}
