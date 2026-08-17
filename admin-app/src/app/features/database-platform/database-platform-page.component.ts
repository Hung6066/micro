import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, finalize, of } from 'rxjs';
import {
  HisHopeI18nService,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePermissionService,
  HisHopeStateComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';
import { AdminApiService, DatabaseContinuityAuditEntry, DatabaseContinuityJob, DatabaseContinuityStatus, PlatformResource } from '../../core/services/admin-api.service';

@Component({
  selector: 'app-database-platform-page',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.databasePlatformTitle' | hhTranslate:'Database platform'"
        [subtitle]="'admin.databasePlatformSubtitle' | hhTranslate:'Health, capacity and migration posture'" />

      @if (loading) {
        <hh-state kind="loading" [message]="'admin.loadingDatabasePlatform' | hhTranslate:'Loading database platform...'" />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <button mat-stroked-button type="button" (click)="load()">
            <mat-icon>refresh</mat-icon>
            {{ 'common.retry' | hhTranslate:'Retry' }}
          </button>
        </hh-state>
      } @else if (databases.length === 0) {
        <hh-state kind="empty" icon="storage" [message]="'admin.noDatabaseResources' | hhTranslate:'No database resources reported.'" />
      } @else {
        <section class="platform-summary" aria-label="Database platform summary">
          <div class="summary-card">
            <span class="summary-label">{{ 'admin.databaseCount' | hhTranslate:'Databases' }}</span>
            <strong>{{ databases.length }}</strong>
          </div>
          <div class="summary-card">
            <span class="summary-label">{{ 'admin.databaseHealthy' | hhTranslate:'Healthy' }}</span>
            <strong>{{ healthyCount }}</strong>
          </div>
          <div class="summary-card">
            <span class="summary-label">{{ 'admin.databaseConnections' | hhTranslate:'Connections' }}</span>
            <strong>{{ totalConnections }}</strong>
          </div>
        </section>

        @if (continuity) {
          <section class="continuity-panel" aria-label="Backup and recovery posture">
            <div class="continuity-heading">
              <div>
                <h2>{{ 'admin.databaseContinuityTitle' | hhTranslate:'Backup and recovery' }}</h2>
                <p>{{ 'admin.databaseContinuitySubtitle' | hhTranslate:'PITR, encryption, retention and measured recovery objectives' }}</p>
              </div>
              <span class="status" [class.status-healthy]="continuity.ready">
                {{ continuity.ready ? ('admin.continuityReady' | hhTranslate:'Ready') : ('admin.continuityNotReady' | hhTranslate:'Not ready') }}
              </span>
            </div>
            <div class="continuity-grid">
              <div><span>{{ 'admin.pitr' | hhTranslate:'PITR' }}</span><strong>{{ continuity.pitrEnabled ? ('common.enabled' | hhTranslate:'Enabled') : ('common.disabled' | hhTranslate:'Disabled') }}</strong></div>
              <div><span>{{ 'admin.backupEncryption' | hhTranslate:'Encryption' }}</span><strong>{{ continuity.encryption.provider }}</strong></div>
              <div><span>{{ 'admin.retention' | hhTranslate:'Retention' }}</span><strong>{{ continuity.retentionDays }} {{ 'common.days' | hhTranslate:'days' }}</strong></div>
              <div><span>{{ 'admin.keepLatestBackups' | hhTranslate:'Keep latest per database' }}</span><strong>{{ continuity.keepLastBackupsPerDatabase || 1 }}</strong></div>
              <div><span>RPO / RTO</span><strong>{{ continuity.targetRpoMinutes }}m / {{ continuity.targetRtoMinutes }}m</strong></div>
              <div><span>Vault Transit</span><strong [class.job-error]="!continuity.vault?.reachable">{{ continuity.vault?.reachable ? ('common.healthy' | hhTranslate:'Healthy') : ('admin.continuityNotReady' | hhTranslate:'Not ready') }}</strong></div>
              <div><span>{{ 'admin.lastBackup' | hhTranslate:'Last successful backup' }}</span><strong>{{ continuity.lastSuccessfulBackupAt ? (continuity.lastSuccessfulBackupAt | date:'medium') : ('admin.never' | hhTranslate:'Never') }}</strong></div>
              <div><span>{{ 'admin.lastRestoreDrill' | hhTranslate:'Last successful restore drill' }}</span><strong>{{ continuity.lastSuccessfulRestoreDrillAt ? (continuity.lastSuccessfulRestoreDrillAt | date:'medium') : ('admin.never' | hhTranslate:'Never') }}</strong></div>
              <div><span>{{ 'admin.schedule' | hhTranslate:'Schedule' }}</span><strong>{{ continuity.schedulerEnabled ? (continuity.backupIntervalHours + 'h / ' + continuity.restoreDrillIntervalHours + 'h') : ('common.disabled' | hhTranslate:'Disabled') }}</strong></div>
              <div><span>{{ 'admin.retryLimit' | hhTranslate:'Retry limit' }}</span><strong>{{ continuity.maxAttempts || 3 }}</strong></div>
            </div>
            @if (continuity.alerts?.length) {
              <div class="continuity-alerts" role="alert">
                <mat-icon>warning</mat-icon>
                <div>
                  <strong>{{ 'admin.continuityAlerts' | hhTranslate:'Continuity alerts' }}</strong>
                  <span>{{ continuity.alerts?.join(', ') }}</span>
                </div>
              </div>
            }
            @if (continuity.configurationErrors.length) {
              <ul class="configuration-errors">
                @for (configurationError of continuity.configurationErrors; track configurationError) { <li>{{ configurationError }}</li> }
              </ul>
            }
            <div class="continuity-actions">
              <button *ngIf="canMutate" mat-stroked-button type="button" [disabled]="actionLoading || !continuity.ready" (click)="requestBackup()">
                <mat-icon>backup</mat-icon>{{ 'admin.requestBackup' | hhTranslate:'Request backup' }}
              </button>
              <button *ngIf="canMutate" mat-stroked-button type="button" [disabled]="actionLoading || !continuity.ready" (click)="requestRestoreDrill()">
                <mat-icon>restore</mat-icon>{{ 'admin.restoreDrill' | hhTranslate:'Run restore drill' }}
              </button>
            </div>
            @if (continuity.latestJob || lastJob) {
              <p class="job-status">
                {{ 'admin.latestContinuityJob' | hhTranslate:'Latest job' }}:
                <strong>{{ (lastJob || continuity.latestJob)?.operation }}</strong>
                <span>{{ (lastJob || continuity.latestJob)?.status }}</span>
                @if ((lastJob || continuity.latestJob)?.errorCode) { <span class="job-error">{{ (lastJob || continuity.latestJob)?.errorCode }}</span> }
              </p>
            }
            @if (actionError) { <p class="action-error">{{ actionError }}</p> }
            <div class="audit-table" aria-label="Backup and restore audit history">
              <div class="audit-heading">
                <div>
                  <h3>{{ 'admin.continuityAuditTitle' | hhTranslate:'Backup / restore audit' }}</h3>
                  <p>{{ 'admin.continuityAuditSubtitle' | hhTranslate:'Durable history of continuity jobs' }}</p>
                </div>
                <button mat-stroked-button type="button" (click)="loadAudit()" [disabled]="auditLoading">
                  <mat-icon>refresh</mat-icon>{{ 'common.refresh' | hhTranslate:'Refresh' }}
                </button>
              </div>
              @if (auditLoading) {
                <p class="audit-empty">{{ 'admin.loadingAudit' | hhTranslate:'Loading audit history...' }}</p>
              } @else if (auditEntries.length === 0) {
                <p class="audit-empty">{{ 'admin.noContinuityAudit' | hhTranslate:'No backup or restore jobs recorded yet.' }}</p>
              } @else {
                <div class="audit-row audit-row-header"><span>Operation</span><span>Status</span><span>Target</span><span>Updated</span><span>Actor</span></div>
                @for (entry of auditEntries; track entry.jobId) {
                  <div class="audit-row">
                    <strong>{{ entry.operation }}</strong>
                    <span [class.audit-complete]="entry.status === 'Completed'" [class.audit-failed]="entry.status === 'Failed'">{{ entry.status }}</span>
                    <span>{{ entry.targetEnvironment }}</span>
                    <small>{{ entry.updatedAt | date:'medium' }}</small>
                    <small>{{ entry.actorSubject || 'system' }}</small>
                    @if (entry.errorCode) { <em>{{ entry.errorCode }}</em> }
                  </div>
                }
              }
            </div>
          </section>
        }

        <section class="resource-grid" aria-label="Database resources">
          @for (database of databases; track database.name) {
            <article class="resource-card">
              <div class="resource-heading">
                <mat-icon>storage</mat-icon>
                <div>
                  <h2>{{ database.displayName || database.name }}</h2>
                  <p>{{ database.engine || 'PostgreSQL' }}</p>
                </div>
                <span class="status" [class.status-healthy]="isHealthy(database)">
                  {{ database.healthStatus || database.status || 'Unknown' }}
                </span>
              </div>
              <dl>
                <div><dt>{{ 'admin.databaseSize' | hhTranslate:'Size' }}</dt><dd>{{ database.sizeMb ?? 0 | number:'1.0-1' }} MB</dd></div>
                <div><dt>{{ 'admin.databaseConnections' | hhTranslate:'Connections' }}</dt><dd>{{ database.connections ?? 0 }}</dd></div>
              </dl>
              @if (database.healthChecks?.length) {
                <ul class="checks">
                  @for (check of database.healthChecks; track check.name) {
                    <li><mat-icon [class.check-ok]="check.status === 'Healthy'">{{ check.status === 'Healthy' ? 'check_circle' : 'error' }}</mat-icon>{{ check.name }}</li>
                  }
                </ul>
              }
            </article>
          }
        </section>
      }
    </hh-page-layout>
  `,
  styles: [`
    .platform-summary { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; margin-bottom:24px; }
    .summary-card,.resource-card { border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); padding:var(--space-5); }
    .summary-card { display:flex; flex-direction:column; gap:8px; }
    .summary-label { color:var(--text-muted); font-size:var(--font-size-caption); }
    .summary-card strong { font-size:1.7rem; }
    .resource-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:16px; }
    .continuity-panel { border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); padding:var(--space-5); margin-bottom:var(--space-6); }
    .continuity-heading { display:flex; justify-content:space-between; gap:16px; align-items:flex-start; }
    .continuity-heading h2 { margin:0; }
    .continuity-heading p { margin:4px 0 0; }
    .continuity-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:12px; margin-top:18px; }
    .continuity-grid div { display:flex; flex-direction:column; gap:var(--space-2); padding:var(--space-3); border-radius:var(--radius-input); background:var(--surface-muted); }
    .continuity-grid span { color:var(--text-muted); font-size:var(--font-size-caption); }
    .continuity-alerts { display:flex; align-items:flex-start; gap:var(--space-2); margin-top:var(--space-4); padding:var(--space-3) var(--space-4); border:1px solid var(--color-danger); border-radius:var(--radius-input); background:var(--surface-danger); color:var(--color-danger); }
    .continuity-alerts mat-icon { color:var(--color-danger); }
    .continuity-alerts div { display:flex; flex-direction:column; gap:3px; }
    .configuration-errors { color:var(--color-danger); margin:var(--space-4) 0 0; }
    .continuity-actions { display:flex; flex-wrap:wrap; gap:12px; margin-top:18px; }
    .job-status { margin:var(--space-4) 0 0; color:var(--text-muted); }
    .job-status span { margin-left:var(--space-2); font-weight:var(--font-weight-semibold); color:var(--color-primary); }
    .job-status .job-error, .action-error { color:var(--color-danger); }
    .action-error { margin:12px 0 0; }
    .audit-table { margin-top:var(--space-5); border-top:1px solid var(--border-default); padding-top:var(--space-4); }
    .audit-heading { display:flex; justify-content:space-between; align-items:flex-start; gap:16px; }
    .audit-heading h3 { margin:0; font-size:1rem; }
    .audit-heading p { margin:4px 0 0; }
    .audit-row { display:grid; grid-template-columns:1.1fr 1fr 1fr 1.3fr 1.5fr; gap:var(--space-3); align-items:center; padding:var(--space-3) 0; border-bottom:1px solid var(--border-default); font-size:var(--font-size-caption); }
    .audit-row-header { color:var(--text-muted); font-size:var(--font-size-caption); font-weight:var(--font-weight-semibold); text-transform:uppercase; }
    .audit-row small { color:var(--text-muted); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .audit-complete { color:var(--color-success); font-weight:var(--font-weight-semibold); }
    .audit-failed, .audit-row em { color:var(--color-danger); font-style:normal; font-weight:var(--font-weight-semibold); }
    .audit-empty { padding:16px 0 0; }
    .resource-heading { display:flex; align-items:flex-start; gap:12px; }
    .resource-heading mat-icon { color:var(--color-primary); }
    h2 { margin:0; font-size:1.05rem; }
    p { margin:var(--space-1) 0 0; color:var(--text-muted); }
    .status { margin-left:auto; border-radius:var(--radius-pill); padding:var(--space-1) var(--space-2); font-size:var(--font-size-caption); background:var(--surface-danger); color:var(--color-danger); }
    .status-healthy { background:var(--surface-success); color:var(--color-success); }
    dl { margin:20px 0 0; display:grid; gap:8px; }
    dl div { display:flex; justify-content:space-between; gap:16px; }
    dt { color:var(--text-muted); } dd { margin:0; font-weight:var(--font-weight-semibold); }
    .checks { list-style:none; padding:var(--space-3) 0 0; margin:var(--space-3) 0 0; border-top:1px solid var(--border-default); display:grid; gap:var(--space-2); }
    .checks li { display:flex; align-items:center; gap:8px; font-size:.85rem; }
    .checks mat-icon { color:var(--color-danger); font-size:18px; height:18px; width:18px; }
    .checks mat-icon.check-ok { color:var(--color-success); }
    @media (max-width: 700px) { .platform-summary,.continuity-grid { grid-template-columns:1fr; } }
  `],
})
export class DatabasePlatformPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  get canMutate(): boolean { return this.permissions.has('admin.settings.write'); }
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  databases: PlatformResource[] = [];
  continuity: DatabaseContinuityStatus | null = null;
  lastJob: DatabaseContinuityJob | null = null;
  loading = false;
  error: string | null = null;
  actionLoading = false;
  actionError: string | null = null;
  auditEntries: DatabaseContinuityAuditEntry[] = [];
  auditLoading = false;

  get healthyCount(): number { return this.databases.filter(database => this.isHealthy(database)).length; }
  get totalConnections(): number { return this.databases.reduce((total, database) => total + (database.connections ?? 0), 0); }

  ngOnInit(): void { this.load(); this.loadContinuity(); this.loadAudit(); }

  load(): void {
    this.loading = true;
    this.error = null;
    this.api.getPlatformResources().pipe(
      finalize(() => { this.loading = false; this.cdr.markForCheck(); }),
      catchError(() => {
        this.error = this.i18n.t('admin.databasePlatformLoadFailed', 'Unable to load database platform resources.');
        return of([] as PlatformResource[]);
      }),
    ).subscribe(resources => { this.databases = resources; this.cdr.markForCheck(); });
  }

  loadContinuity(): void {
    this.api.getDatabaseContinuity().subscribe({
      next: status => { this.continuity = status; this.cdr.markForCheck(); },
      error: () => { this.continuity = null; this.cdr.markForCheck(); },
    });
  }

  loadAudit(): void {
    this.auditLoading = true;
    this.api.getDatabaseContinuityAudit().subscribe({
      next: entries => { this.auditEntries = entries; this.auditLoading = false; this.cdr.markForCheck(); },
      error: () => { this.auditEntries = []; this.auditLoading = false; this.cdr.markForCheck(); },
    });
  }

  requestBackup(): void {
    if (!this.canMutate) return;
    this.actionLoading = true;
    this.actionError = null;
    this.api.requestDatabaseBackup().subscribe({
      next: job => { this.lastJob = job; this.actionLoading = false; this.cdr.markForCheck(); this.loadContinuity(); this.loadAudit(); },
      error: error => { this.actionError = error?.error?.detail || this.i18n.t('admin.continuityRequestFailed', 'Unable to request backup.'); this.actionLoading = false; this.cdr.markForCheck(); },
    });
  }

  requestRestoreDrill(): void {
    if (!this.canMutate) return;
    if (!window.confirm(this.i18n.t('admin.restoreDrillConfirm', 'Run a restore drill in the isolated staging environment?'))) return;
    this.actionLoading = true;
    this.actionError = null;
    this.api.requestRestoreDrill().subscribe({
      next: job => { this.lastJob = job; this.actionLoading = false; this.cdr.markForCheck(); this.loadContinuity(); this.loadAudit(); },
      error: error => { this.actionError = error?.error?.detail || this.i18n.t('admin.continuityRequestFailed', 'Unable to request restore drill.'); this.actionLoading = false; this.cdr.markForCheck(); },
    });
  }

  isHealthy(resource: PlatformResource): boolean {
    return (resource.healthStatus || resource.status || '').toLowerCase() === 'healthy';
  }
}
