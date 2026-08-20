import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeFileUploadComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  AdminSession,
  BulkImportPreview,
  BulkImportResult,
  User,
} from "../../core/contracts/admin.contracts";
import { IdentityOperationsApiService } from "../../core/services/identity-operations-api.service";
import { catchError, of, tap } from "rxjs";
import { UsersApiService } from "../../core/services/users-api.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-identity-operations-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeFileUploadComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          'admin.identityOperations' | hhTranslate: 'Identity operations'
        "
        [subtitle]="
          'admin.identityOperationsSubtitle'
            | hhTranslate
              : 'Incident response, lifecycle import and outbox operations'
        "
      />
      <p class="notice">
        {{
          "admin.identityOperationsNotice"
            | hhTranslate
              : "All actions are audited. Secrets and credential material remain server-side."
        }}
      </p>
      <section class="grid two-col" [formGroup]="formGroup">
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.sessionControls" | hhTranslate: "Session controls"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.userId" | hhTranslate: "User ID"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.userId"
                ><mat-option value="">{{
                  "admin.select" | hhTranslate: "Select"
                }}</mat-option
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.reason" | hhTranslate: "Reason"
              }}</mat-label
              ><textarea
                matInput
                rows="2"
                [formControl]="formGroup.controls.reason"
              ></textarea>
            </mat-form-field>
            <div class="actions">
              <hh-action-button
                [disabled]="
                  busy ||
                  !formGroup.controls.userId.value ||
                  !can('admin.sessions.read')
                "
                (pressed)="loadSessions()"
                kind="secondary"
                icon="refresh"
                [label]="'admin.loadSessions' | hhTranslate: 'Load sessions'"
              /><hh-action-button
                [disabled]="
                  busy || !userId || !reason || !can('admin.sessions.revoke')
                "
                (pressed)="revokeAllSessions()"
                kind="danger"
                icon="block"
                [label]="
                  'admin.revokeAllSessions' | hhTranslate: 'Revoke all sessions'
                "
              />
            </div>
            <hh-data-table
              *ngIf="sessions.length"
              [label]="'admin.sessionControls' | hhTranslate"
              [columns]="sessionColumns"
              [rows]="sessionRows"
              [loading]="resource.loading()"
              [empty]="false"
              ><ng-template hhDataTableCell="actions" let-row
                ><hh-action-button
                  kind="danger"
                  mode="icon-only"
                  icon="link_off"
                  [label]="'admin.revoke' | hhTranslate: 'Revoke'"
                  [disabled]="busy || !can('admin.sessions.revoke')"
                  (pressed)="revokeSessionByRow(row)" /></ng-template
            ></hh-data-table> </mat-card-content
        ></mat-card>
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.credentialReset" | hhTranslate: "Credential reset"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <p class="muted">
              {{
                "admin.credentialResetNotice"
                  | hhTranslate
                    : "Reset invalidates existing tokens and requires the user to authenticate again."
              }}
            </p>
            <mat-checkbox [formControl]="formGroup.controls.resetMfa">{{
              "admin.resetMfa" | hhTranslate: "Reset MFA"
            }}</mat-checkbox
            ><mat-checkbox [formControl]="formGroup.controls.revokePasskeys">{{
              "admin.revokePasskeys" | hhTranslate: "Revoke passkeys"
            }}</mat-checkbox
            ><hh-action-button
              [disabled]="
                busy ||
                !userId ||
                !reason ||
                (!formGroup.controls.resetMfa.value &&
                  !formGroup.controls.revokePasskeys.value) ||
                !can('admin.credentials.reset')
              "
              (pressed)="resetCredentials()"
              kind="danger"
              icon="lock_reset"
              [label]="
                'admin.resetCredentials'
                  | hhTranslate: 'Reset selected credentials'
              "
            /> </mat-card-content
        ></mat-card>
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.bulkImport" | hhTranslate: "Bulk user import"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <hh-file-upload
              accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              [maxSizeBytes]="10 * 1024 * 1024"
              [label]="'admin.bulkImport' | hhTranslate: 'Bulk user import'"
              [hint]="
                'admin.bulkImportLimit'
                  | hhTranslate: 'CSV/XLSX, maximum 10 MB and 10,000 users.'
              "
              (filesChange)="selectFile($event)"
            />
            <p class="muted">
              {{
                "admin.bulkImportLimit"
                  | hhTranslate: "CSV/XLSX, maximum 10 MB and 10,000 users."
              }}
            </p>
            <div class="actions">
              <hh-action-button
                [disabled]="busy || !file || !can('admin.users.read')"
                (pressed)="previewImport()"
                kind="secondary"
                icon="preview"
                [label]="'admin.previewImport' | hhTranslate: 'Preview'"
              /><hh-action-button
                [disabled]="busy || !file || !can('admin.users.write')"
                (pressed)="executeImport()"
                kind="primary"
                icon="play_arrow"
                [label]="'admin.executeImport' | hhTranslate: 'Execute import'"
              />
            </div>
            <p *ngIf="preview">
              {{ preview.valid }}/{{ preview.total }}
              {{ "admin.validRows" | hhTranslate: "valid rows" }}
            </p>
            <p *ngIf="importResult">
              {{ importResult.created }}
              {{ "admin.created" | hhTranslate: "created" }},
              {{ importResult.skipped }}
              {{ "admin.skipped" | hhTranslate: "skipped" }},
              {{ importResult.failed }}
              {{ "admin.failed" | hhTranslate: "failed" }}
            </p>
          </mat-card-content></mat-card
        >
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.outboxOperations" | hhTranslate: "Outbox operations"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.provisioningTarget" | hhTranslate: "Provisioning target"
              }}</mat-label
              ><mat-select [formControl]="formGroup.controls.provisioningTarget"
                ><mat-option value="scim">{{
                  "admin.provisioningScim" | hhTranslate: "SCIM"
                }}</mat-option
                ><mat-option value="entra">{{
                  "admin.provisioningEntra" | hhTranslate: "Microsoft Entra ID"
                }}</mat-option
                ><mat-option value="google-workspace">{{
                  "admin.provisioningGoogleWorkspace"
                    | hhTranslate: "Google Workspace"
                }}</mat-option></mat-select
              ></mat-form-field
            ><hh-action-button
              [disabled]="busy || !can('admin.provisioning.manage')"
              (pressed)="reconcile()"
              kind="secondary"
              icon="sync"
              [label]="'admin.reconcile' | hhTranslate: 'Queue full reconcile'"
            /><mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.ssfOutboxId" | hhTranslate: "SSF outbox ID"
              }}</mat-label
              ><input
                matInput
                [formControl]="formGroup.controls.ssfId" /></mat-form-field
            ><hh-action-button
              [disabled]="
                busy || !ssfId || !can('admin.security-signals.manage')
              "
              (pressed)="retrySsf()"
              kind="secondary"
              icon="replay"
              [label]="'admin.retrySsf' | hhTranslate: 'Retry SSF delivery'"
            /> </mat-card-content
        ></mat-card>
      </section>
      <p class="error" *ngIf="error">{{ error }}</p>
    </hh-page-layout>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .grid {
        display: grid;
        gap: var(--space-4);
        margin-top: var(--space-4);
      }
      .two-col {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .form-grid {
        display: grid;
        gap: var(--space-3);
      }
      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-2);
      }
      .notice,
      .muted {
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .notice {
        padding: 12px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-muted);
      }
      .table-wrap {
        overflow: auto;
      }
      table {
        width: 100%;
        border-collapse: collapse;
        font-size: var(--font-size-caption);
      }
      th,
      td {
        text-align: left;
        padding: 8px;
        border-bottom: 1px solid var(--border-subtle);
        white-space: nowrap;
      }
      .mono {
        font-family: var(--font-mono);
        font-size: 11px;
      }
      .error {
        color: var(--color-danger);
      }
      @media (max-width: 800px) {
        .two-col {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class IdentityOperationsPageComponent {
  private readonly api = inject(IdentityOperationsApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{ sessions: AdminSession[] }>(
    this.destroyRef,
  );
  readonly formGroup = new FormGroup({
    userId: new FormControl("", { nonNullable: true }),
    reason: new FormControl("", { nonNullable: true }),
    resetMfa: new FormControl(true, { nonNullable: true }),
    revokePasskeys: new FormControl(true, { nonNullable: true }),
    provisioningTarget: new FormControl<"scim" | "entra" | "google-workspace">(
      "scim",
      { nonNullable: true },
    ),
    ssfId: new FormControl("", { nonNullable: true }),
  });
  users: User[] = [];
  sessions: AdminSession[] = [];
  file?: File;
  preview?: BulkImportPreview;
  importResult?: BulkImportResult;
  busy = false;
  error = "";
  get userId(): string {
    return this.formGroup.controls.userId.value;
  }
  get reason(): string {
    return this.formGroup.controls.reason.value;
  }
  get ssfId(): string {
    return this.formGroup.controls.ssfId.value;
  }
  get sessionRows(): Record<string, unknown>[] {
    return this.sessions.map((session) => ({
      ...session,
      displayStatus: session.active
        ? this.i18n.t("admin.active", "active")
        : this.i18n.t("admin.expired", "expired"),
    }));
  }
  get sessionColumns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "id", label: this.i18n.t("admin.id", "ID") },
      {
        key: "expiresAt",
        label: this.i18n.t("admin.expires", "Expires"),
        format: "dateTime",
      },
      {
        key: "displayStatus",
        label: this.i18n.t("admin.status", "Status"),
      },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
      },
    ];
  }
  constructor() {
    this.usersApi.getUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.cdr.markForCheck();
      },
      error: () => {
        this.users = [];
        this.cdr.markForCheck();
      },
    });
  }

  can(permission: string): boolean {
    return this.permissions.has(permission);
  }

  loadSessions(): void {
    if (!this.can("admin.sessions.read")) return;
    this.error = "";
    this.resource.load(
      this.api.getAdminSessions(this.userId).pipe(
        tap((result) => {
          this.sessions = result.sessions;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.identityOperationFailed",
            "Unable to load identity sessions.",
          );
          return of({ sessions: [] });
        }),
      ),
    );
  }
  revokeSession(session: AdminSession): void {
    if (!this.can("admin.sessions.revoke")) return;
    this.run(
      () => this.api.revokeAdminSession(this.userId, session.id, this.reason),
      () => {
        this.sessions = this.sessions.filter((item) => item.id !== session.id);
      },
      "admin.sessionRevoked",
      "Session revoked.",
      "admin.iamSaveFailed",
      "Unable to revoke session.",
    );
  }
  revokeSessionByRow(row: Record<string, unknown>): void {
    const session = this.sessions.find((item) => item.id === row["id"]);
    if (session) {
      this.revokeSession(session);
    }
  }
  revokeAllSessions(): void {
    if (!this.can("admin.sessions.revoke")) return;
    this.run(
      () => this.api.revokeAllAdminSessions(this.userId, this.reason),
      () => {
        this.sessions = [];
      },
      "admin.allSessionsRevoked",
      "All sessions revoked.",
      "admin.iamSaveFailed",
      "Unable to revoke all sessions.",
    );
  }
  resetCredentials(): void {
    if (!this.can("admin.credentials.reset")) return;
    this.run(
      () =>
        this.api.resetAdminCredentials(this.userId, {
          resetMfa: this.formGroup.controls.resetMfa.value,
          revokePasskeys: this.formGroup.controls.revokePasskeys.value,
          reason: this.reason,
        }),
      () => undefined,
      "admin.credentialsReset",
      "Credentials reset.",
      "admin.identityOperationFailed",
      "Unable to reset credentials.",
    );
  }
  selectFile(files: File[]): void {
    this.file = files[0];
    this.preview = undefined;
    this.importResult = undefined;
  }
  previewImport(): void {
    if (!this.file || !this.can("admin.users.read")) return;
    this.run(
      () => this.api.previewUserImport(this.file!),
      (result) => {
        this.preview = result as BulkImportPreview;
      },
    );
  }
  executeImport(): void {
    if (!this.file || !this.can("admin.users.write")) return;
    this.run(
      () => this.api.importUsers(this.file!),
      (result) => {
        this.importResult = result;
      },
      "admin.importCompleted",
      "Import completed.",
      "admin.identityOperationFailed",
      "Unable to execute import.",
    );
  }
  reconcile(): void {
    if (!this.can("admin.provisioning.manage")) return;
    this.run(
      () =>
        this.api.reconcileProvisioning(
          this.formGroup.controls.provisioningTarget.value,
        ),
      () => undefined,
      "admin.reconcileQueued",
      "Provisioning reconcile queued.",
      "admin.identityOperationFailed",
      "Unable to queue reconcile.",
    );
  }
  retrySsf(): void {
    if (!this.can("admin.security-signals.manage")) return;
    this.run(
      () => this.api.retrySecuritySignal(this.ssfId),
      () => {
        this.formGroup.patchValue({ ssfId: "" });
      },
      "admin.ssfRetryQueued",
      "SSF retry queued.",
      "admin.identityOperationFailed",
      "Unable to retry SSF delivery.",
    );
  }

  private run<T>(
    request: () => import("rxjs").Observable<T>,
    onSuccess: (value: T) => void,
    successKey?: string,
    successFallback?: string,
    errorKey = "admin.identityOperationFailed",
    errorFallback = "Identity operation was rejected.",
  ): void {
    this.busy = true;
    this.error = "";
    this.cdr.markForCheck();
    request().subscribe({
      next: (value) => {
        this.busy = false;
        onSuccess(value);
        if (successKey && successFallback) {
          this.toast.success(this.i18n.t(successKey, successFallback), {
            duration: 3000,
          });
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = this.i18n.t(errorKey, errorFallback);
        this.toast.error(this.error, { duration: 5000 });
        this.busy = false;
        this.cdr.markForCheck();
      },
    });
  }
}
