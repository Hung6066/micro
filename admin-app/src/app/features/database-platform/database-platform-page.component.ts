import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { catchError, of, tap } from "rxjs";

import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeConfirmDialogComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  DatabaseContinuityAuditEntry,
  DatabaseContinuityJob,
  DatabaseContinuityStatus,
  DatabasePlatformApiService,
  PlatformResource,
} from "../../core/services/database-platform-api.service";
import { DatabasePlatformOverviewComponent } from "./database-platform-overview.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { AdminConfirmState } from "../../core/services/admin-confirm-state";
@Component({
  selector: "app-database-platform-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatButtonModule,
    MatIconModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeConfirmDialogComponent,
    HisHopeTranslatePipe,
    DatabasePlatformOverviewComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          'admin.databasePlatformTitle' | hhTranslate: 'Database platform'
        "
        [subtitle]="
          'admin.databasePlatformSubtitle'
            | hhTranslate: 'Health, capacity and migration posture'
        "
      />

      @if (loading) {
        <hh-state
          kind="loading"
          [message]="
            'admin.loadingDatabasePlatform'
              | hhTranslate: 'Loading database platform...'
          "
        />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <hh-action-button
            (pressed)="load()"
            kind="secondary"
            icon="refresh"
            [label]="'common.retry' | hhTranslate: 'Retry'"
          />
        </hh-state>
      } @else if (databases.length === 0) {
        <hh-state
          kind="empty"
          icon="storage"
          [message]="
            'admin.noDatabaseResources'
              | hhTranslate: 'No database resources reported.'
          "
        />
      } @else {
        <app-database-platform-overview
          [databases]="databases"
          [continuity]="continuity"
          [lastJob]="lastJob"
          [canMutate]="canMutate"
          [actionLoading]="actionLoading"
          [actionError]="actionError"
          [auditEntries]="auditEntries"
          [auditLoading]="auditLoading"
          (backupRequested)="requestBackup()"
          (restoreDrillRequested)="requestRestoreDrill()"
          (auditRefreshRequested)="loadAudit()"
        />
      }
      <hh-confirm-dialog
        [open]="confirm.open"
        [title]="confirm.title"
        [message]="confirm.message"
        [confirmLabel]="confirm.confirmLabel"
        (confirmed)="confirm.confirm()"
        (cancelled)="confirm.cancel()"
      />
    </hh-page-layout>
  `,
  styles: [
    `
      .platform-summary {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--space-lg);
        margin-bottom: var(--space-2xl);
      }
      .summary-card,
      .resource-card {
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        padding: var(--space-xl);
      }
      .summary-card {
        display: flex;
        flex-direction: column;
        gap: var(--space-sm);
      }
      .summary-label {
        color: var(--text-muted);
        font-size: var(--font-size-caption);
      }
      .summary-card strong {
        font-size: 1.7rem;
      }
      .resource-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        gap: var(--space-lg);
      }
      .continuity-panel {
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        padding: var(--space-xl);
        margin-bottom: var(--space-2xl);
      }
      .continuity-heading {
        display: flex;
        justify-content: space-between;
        gap: var(--space-lg);
        align-items: flex-start;
      }
      .continuity-heading h2 {
        margin: 0;
      }
      .continuity-heading p {
        margin: var(--space-2xs) 0 0;
      }
      .continuity-grid {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--space-md);
        margin-top: 18px;
      }
      .continuity-grid div {
        display: flex;
        flex-direction: column;
        gap: var(--space-sm);
        padding: var(--space-md);
        border-radius: var(--radius-input);
        background: var(--surface-muted);
      }
      .continuity-grid span {
        color: var(--text-muted);
        font-size: var(--font-size-caption);
      }
      .continuity-alerts {
        display: flex;
        align-items: flex-start;
        gap: var(--space-sm);
        margin-top: var(--space-lg);
        padding: var(--space-md) var(--space-lg);
        border: 1px solid var(--color-danger);
        border-radius: var(--radius-input);
        background: var(--surface-danger);
        color: var(--color-danger);
      }
      .continuity-alerts mat-icon {
        color: var(--color-danger);
      }
      .continuity-alerts div {
        display: flex;
        flex-direction: column;
        gap: var(--space-xxs);
      }
      .configuration-errors {
        color: var(--color-danger);
        margin: var(--space-lg) 0 0;
      }
      .continuity-actions {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-md);
        margin-top: 18px;
      }
      .job-status {
        margin: var(--space-lg) 0 0;
        color: var(--text-muted);
      }
      .job-status span {
        margin-left: var(--space-sm);
        font-weight: var(--font-weight-semibold);
        color: var(--color-primary);
      }
      .job-status .job-error,
      .action-error {
        color: var(--color-danger);
      }
      .action-error {
        margin: var(--space-md) 0 0;
      }
      .audit-table {
        margin-top: var(--space-xl);
        border-top: 1px solid var(--border-default);
        padding-top: var(--space-lg);
      }
      .audit-heading {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: var(--space-lg);
      }
      .audit-heading h3 {
        margin: 0;
        font-size: 1rem;
      }
      .audit-heading p {
        margin: var(--space-2xs) 0 0;
      }
      .audit-row {
        display: grid;
        grid-template-columns: 1.1fr 1fr 1fr 1.3fr 1.5fr;
        gap: var(--space-md);
        align-items: center;
        padding: var(--space-md) 0;
        border-bottom: 1px solid var(--border-default);
        font-size: var(--font-size-caption);
      }
      .audit-row-header {
        color: var(--text-muted);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-semibold);
        text-transform: uppercase;
      }
      .audit-row small {
        color: var(--text-muted);
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .audit-complete {
        color: var(--color-success);
        font-weight: var(--font-weight-semibold);
      }
      .audit-failed,
      .audit-row em {
        color: var(--color-danger);
        font-style: normal;
        font-weight: var(--font-weight-semibold);
      }
      .audit-empty {
        padding: var(--space-lg) 0 0;
      }
      .resource-heading {
        display: flex;
        align-items: flex-start;
        gap: var(--space-md);
      }
      .resource-heading mat-icon {
        color: var(--color-primary);
      }
      h2 {
        margin: 0;
        font-size: 1.05rem;
      }
      p {
        margin: var(--space-2xs) 0 0;
        color: var(--text-muted);
      }
      .status {
        margin-left: auto;
        border-radius: var(--radius-pill);
        padding: var(--space-2xs) var(--space-sm);
        font-size: var(--font-size-caption);
        background: var(--surface-danger);
        color: var(--color-danger);
      }
      .status-healthy {
        background: var(--surface-success);
        color: var(--color-success);
      }
      dl {
        margin: var(--space-xl) 0 0;
        display: grid;
        gap: var(--space-sm);
      }
      dl div {
        display: flex;
        justify-content: space-between;
        gap: var(--space-lg);
      }
      dt {
        color: var(--text-muted);
      }
      dd {
        margin: 0;
        font-weight: var(--font-weight-semibold);
      }
      .checks {
        list-style: none;
        padding: var(--space-md) 0 0;
        margin: var(--space-md) 0 0;
        border-top: 1px solid var(--border-default);
        display: grid;
        gap: var(--space-sm);
      }
      .checks li {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        font-size: 0.85rem;
      }
      .checks mat-icon {
        color: var(--color-danger);
        font-size: var(--font-size-icon-sm);
        height: 18px;
        width: 18px;
      }
      .checks mat-icon.check-ok {
        color: var(--color-success);
      }
      @media (max-width: 700px) {
        .platform-summary,
        .continuity-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class DatabasePlatformPageComponent implements OnInit {
  private readonly api = inject(DatabasePlatformApiService);
  private readonly permissions = inject(HisHopePermissionService);
  get canMutate(): boolean {
    return this.permissions.has("admin.settings.write");
  }
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly confirm = new AdminConfirmState();
  readonly resource = new HisHopeResourceState<PlatformResource[]>(
    this.destroyRef,
  );
  databases: PlatformResource[] = [];
  continuity: DatabaseContinuityStatus | null = null;
  lastJob: DatabaseContinuityJob | null = null;
  get loading(): boolean {
    return this.resource.loading();
  }
  error: string | null = null;
  actionLoading = false;
  actionError: string | null = null;
  auditEntries: DatabaseContinuityAuditEntry[] = [];
  auditLoading = false;

  get healthyCount(): number {
    return this.databases.filter((database) => this.isHealthy(database)).length;
  }
  get totalConnections(): number {
    return this.databases.reduce(
      (total, database) => total + (database.connections ?? 0),
      0,
    );
  }

  ngOnInit(): void {
    this.load();
    this.loadContinuity();
    this.loadAudit();
  }

  load(): void {
    this.error = null;
    this.resource.load(
      this.api.getPlatformResources().pipe(
        tap((resources) => {
          this.databases = resources;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.databasePlatformLoadFailed",
            "Unable to load database platform resources.",
          );
          return of([] as PlatformResource[]);
        }),
      ),
    );
  }

  loadContinuity(): void {
    this.api.getDatabaseContinuity().subscribe({
      next: (status) => {
        this.continuity = status;
        this.cdr.markForCheck();
      },
      error: () => {
        this.continuity = null;
        this.cdr.markForCheck();
      },
    });
  }

  loadAudit(): void {
    this.auditLoading = true;
    this.api.getDatabaseContinuityAudit().subscribe({
      next: (entries) => {
        this.auditEntries = entries;
        this.auditLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.auditEntries = [];
        this.auditLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  requestBackup(): void {
    if (!this.canMutate) return;
    this.actionLoading = true;
    this.actionError = null;
    this.api.requestDatabaseBackup().subscribe({
      next: (job) => {
        this.lastJob = job;
        this.actionLoading = false;
        this.cdr.markForCheck();
        this.loadContinuity();
        this.loadAudit();
      },
      error: (error) => {
        this.actionError =
          error?.error?.detail ||
          this.i18n.t(
            "admin.continuityRequestFailed",
            "Unable to request backup.",
          );
        this.actionLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  requestRestoreDrill(): void {
    if (!this.canMutate) return;
    this.confirm.ask(
      this.i18n.t(
        "admin.restoreDrillConfirm",
        "Run a restore drill in the isolated staging environment?",
      ),
      () => this.commitRestoreDrill(),
    );
  }

  private commitRestoreDrill(): void {
    this.actionLoading = true;
    this.actionError = null;
    this.api.requestRestoreDrill().subscribe({
      next: (job) => {
        this.lastJob = job;
        this.actionLoading = false;
        this.cdr.markForCheck();
        this.loadContinuity();
        this.loadAudit();
      },
      error: (error) => {
        this.actionError =
          error?.error?.detail ||
          this.i18n.t(
            "admin.continuityRequestFailed",
            "Unable to request restore drill.",
          );
        this.actionLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  isHealthy(resource: PlatformResource): boolean {
    return (
      (resource.healthStatus || resource.status || "").toLowerCase() ===
      "healthy"
    );
  }
}
