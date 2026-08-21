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
  AccessRequest,
  Role,
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
  selector: "app-jit-access-page",
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
        [title]="'admin.jitAccess' | hhTranslate: 'JIT access'"
        [subtitle]="
          'admin.jitAccessSubtitle'
            | hhTranslate: 'Time-limited elevation with explicit expiry.'
        "
        ><hh-action-button
          [disabled]="busy"
          (pressed)="load()"
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate: 'Refresh'"
      /></hh-page-header>
      <p class="notice">
        {{
          "admin.jitBoundary"
            | hhTranslate
              : "JIT is implemented by the server access-request workflow with expiry, MFA, maker-checker and audit enforcement."
        }}
      </p>
      <p class="error" *ngIf="error">{{ error }}</p>
      <mat-card
        ><mat-card-header
          ><mat-card-title>{{
            "admin.requestJitAccess" | hhTranslate: "Request JIT access"
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
              ><mat-label>{{ "admin.roles" | hhTranslate: "Roles" }}</mat-label
              ><mat-select multiple [formControl]="formGroup.controls.roleIds"
                ><mat-option *ngFor="let role of roles" [value]="role.id">{{
                  role.name
                }}</mat-option></mat-select
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
            ><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.expiryHours" | hhTranslate: "Expiry hours"
              }}</mat-label
              ><input
                matInput
                type="number"
                min="1"
                max="72"
                [formControl]="
                  formGroup.controls.expiryHours
                " /></mat-form-field
            ><hh-action-button
              [disabled]="busy || !canWrite || !canSubmit"
              (pressed)="create()"
              kind="primary"
              icon="add"
              [label]="
                'admin.createJitRequest' | hhTranslate: 'Create JIT request'
              "
            /></form></mat-card-content
      ></mat-card>
      <mat-card class="table-card"
        ><mat-card-header
          ><mat-card-title>{{
            "admin.activeJitRequests" | hhTranslate: "JIT requests"
          }}</mat-card-title></mat-card-header
        ><mat-card-content
          ><hh-data-table
            [label]="'admin.activeJitRequests' | hhTranslate"
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
                  *ngIf="row['status'] === 'pending'"
                  kind="danger"
                  mode="icon-only"
                  icon="close"
                  [label]="'admin.reject' | hhTranslate: 'Reject'"
                  [disabled]="busy || !canWrite"
                  (pressed)="rejectByRow(row)"
                /></div></ng-template></hh-data-table></mat-card-content
      ></mat-card>
    </hh-page-layout>
  `,
  styles: [
    ":host{display:block}.form-grid{display:grid;gap:var(--space-md)}.notice{padding:var(--space-md);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-lg)}.action-cell{display:flex;gap:var(--space-sm)}",
  ],
})
export class JitAccessPageComponent implements OnInit {
  private readonly api = inject(AccessGovernanceApiService);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    users: User[];
    roles: Role[];
    requests: AccessRequest[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.loadAccessGovernanceFailed",
    loadErrorFallback: "Unable to load JIT data.",
  });
  users: User[] = [];
  roles: Role[] = [];
  requests: AccessRequest[] = [];
  busy = false;
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  readonly formGroup = new FormGroup({
    subjectUserId: new FormControl("", { nonNullable: true }),
    roleIds: new FormControl<string[]>([], { nonNullable: true }),
    reason: new FormControl("", { nonNullable: true }),
    expiryHours: new FormControl(8, { nonNullable: true }),
  });
  get canWrite(): boolean {
    return this.permissionService.has("admin.roles.write");
  }
  get canSubmit(): boolean {
    const value = this.formGroup.getRawValue();
    return (
      !!value.subjectUserId &&
      value.roleIds.length > 0 &&
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
        roles: this.api.getRoles(),
        requests: this.api.getAccessRequests(),
      }).pipe(
        tap((state) => {
          this.users = state.users;
          this.roles = state.roles;
          this.requests = state.requests.filter(
            (x) => new Date(x.expiresAt).getTime() > Date.now(),
          );
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadAccessGovernanceFailed",
            "Unable to load JIT data.",
          );
          return of({ users: [], roles: [], requests: [] });
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
    this.busy = true;
    this.cdr.markForCheck();
    this.api.createAccessRequest(this.formGroup.getRawValue()).subscribe({
      next: () => {
        this.busy = false;
        this.toast.success(
          this.i18n.t("admin.jitCreated", "JIT request created."),
          { duration: 3000 },
        );
        this.formGroup.reset({
          subjectUserId: "",
          roleIds: [],
          reason: "",
          expiryHours: 8,
        });
        this.cdr.markForCheck();
        this.load();
      },
      error: () =>
        this.fail("admin.jitCreateFailed", "Unable to create JIT request."),
    });
  }
  approve(item: AccessRequest): void {
    this.mutate(
      this.api.approveAccessRequest(item.id),
      "admin.accessRequestApproved",
      "JIT request approved.",
    );
  }
  reject(item: AccessRequest): void {
    this.mutate(
      this.api.rejectAccessRequest(item.id),
      "admin.accessRequestRejected",
      "JIT request rejected.",
    );
  }
  approveByRow(row: Record<string, unknown>): void {
    const item = this.requests.find((request) => request.id === row["id"]);
    if (item) {
      this.approve(item);
    }
  }
  rejectByRow(row: Record<string, unknown>): void {
    const item = this.requests.find((request) => request.id === row["id"]);
    if (item) {
      this.reject(item);
    }
  }
  private mutate(
    operation: import("rxjs").Observable<unknown>,
    key: string,
    fallback: string,
  ): void {
    if (!this.canWrite) return;
    this.busy = true;
    this.cdr.markForCheck();
    operation.subscribe({
      next: () => {
        this.busy = false;
        this.toast.success(this.i18n.t(key, fallback), { duration: 3000 });
        this.cdr.markForCheck();
        this.load();
      },
      error: () => this.fail("admin.jitMutationFailed", fallback),
    });
  }
  private fail(key: string, fallback: string): void {
    this.error = this.i18n.t(key, fallback);
    this.toast.error(this.error, { duration: 5000 });
    this.busy = false;
    this.cdr.markForCheck();
  }
}
