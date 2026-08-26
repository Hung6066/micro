import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";

import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, forkJoin, of, tap } from "rxjs";
import { IamApiService } from "../../core/services/iam-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  IamAuditIntegrations,
  SecuritySignalOutboxEntry,
} from "../../core/contracts/admin.contracts";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-workload-sessions-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="
        'admin.workloadSessions' | hhTranslate: 'Workload sessions'
      " /><hh-toolbar hhPageToolbar
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <hh-data-table
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite"
          (pressed)="revoke(row)"
          kind="danger"
          mode="icon-only"
          icon="link_off"
          [label]="'admin.revoke' | hhTranslate" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class IamWorkloadSessionsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{
    sessions: Record<string, unknown>[];
  }>(this.destroyRef);
  get canWrite() {
    return this.permissions.has("admin.roles.write");
  }
  rows: Record<string, unknown>[] = [];
  get loading(): boolean {
    return this.resource.loading();
  }
  error = "";
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "sessionId", label: this.i18n.t("admin.sessionId", "Session ID") },
      {
        key: "workloadRoleKey",
        label: this.i18n.t("admin.workloadRole", "Workload role"),
      },
      { key: "audience", label: this.i18n.t("admin.audience", "Audience") },
      {
        key: "expiresAt",
        label: this.i18n.t("admin.expiresAt", "Expires"),
        format: "dateTime",
      },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
      },
    ];
  }
  ngOnInit() {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load() {
    this.error = "";
    this.resource.load(
      this.api.getIamWorkloadSessionCatalog().pipe(
        tap((x) => {
          this.rows = x.sessions.map((item) => ({ ...item }));
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load workload sessions.",
          );
          return of({ sessions: [] });
        }),
      ),
    );
  }
  revoke(row: Record<string, unknown>) {
    const roleId = String(row["workloadRoleId"] ?? ""),
      id = String(row["sessionId"] ?? "");
    if (!this.canWrite || !roleId || !id) return;
    this.api.revokeIamWorkloadSession(roleId, id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to revoke workload session.",
        )),
    });
  }
}

@Component({
  selector: "app-iam-audit-integrations-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="
        'admin.auditIntegrations' | hhTranslate: 'Audit & integrations'
      " /><hh-toolbar hhPageToolbar
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <section *ngIf="integration" class="hh-form-card">
      <p>
        Audit append-only: <strong>{{ integration.audit.appendOnly }}</strong>
      </p>
      <p>
        SSF: <strong>{{ integration.ssf.enabled }}</strong> ·
        {{ integration.ssf.pending }} pending
      </p>
    </section>
    <hh-data-table
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite"
          (pressed)="retry(row)"
          kind="secondary"
          icon="refresh"
          [label]="'admin.retry' | hhTranslate" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class IamAuditIntegrationsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{
    integration: IamAuditIntegrations | null;
    outbox: SecuritySignalOutboxEntry[];
  }>(this.destroyRef);
  get canWrite() {
    return this.permissions.has("admin.settings.write");
  }
  integration: IamAuditIntegrations | null = null;
  rows: Record<string, unknown>[] = [];
  get loading(): boolean {
    return this.resource.loading();
  }
  error = "";
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "eventType", label: this.i18n.t("admin.eventType", "Event") },
      {
        key: "createdAt",
        label: this.i18n.t("admin.createdAt", "Created"),
        format: "dateTime",
      },
      { key: "attempts", label: this.i18n.t("admin.attempts", "Attempts") },
      { key: "lastError", label: this.i18n.t("admin.error", "Error") },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
      },
    ];
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.error = "";
    this.resource.load(
      forkJoin({
        integration: this.api.getIamAuditIntegrations(),
        outbox: this.api.getSecuritySignalOutbox(),
      }).pipe(
        tap((x) => {
          this.integration = x.integration;
          this.rows = x.outbox.map((item) => ({ ...item }));
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load audit integrations.",
          );
          return of({ integration: null, outbox: [] });
        }),
      ),
    );
  }
  retry(row: Record<string, unknown>) {
    const id = String(row["id"] ?? "");
    if (!this.canWrite || !id) return;
    this.api.retrySecuritySignal(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to retry security signal.",
        )),
    });
  }
}
