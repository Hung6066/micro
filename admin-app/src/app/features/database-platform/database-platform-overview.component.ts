import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation";
import {
  DatabaseContinuityAuditEntry,
  DatabaseContinuityJob,
  DatabaseContinuityStatus,
  PlatformResource,
} from "../../core/services/database-platform-api.service";

@Component({
  selector: "app-database-platform-overview",
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="platform-summary" [attr.aria-label]="'admin.databasePlatformSummary' | hhTranslate: 'Database platform summary'">
      <div class="summary-card"><span class="summary-label">{{ "admin.databaseCount" | hhTranslate: "Databases" }}</span><strong>{{ databases.length }}</strong></div>
      <div class="summary-card"><span class="summary-label">{{ "admin.databaseHealthy" | hhTranslate: "Healthy" }}</span><strong>{{ healthyCount }}</strong></div>
      <div class="summary-card"><span class="summary-label">{{ "admin.databaseConnections" | hhTranslate: "Connections" }}</span><strong>{{ totalConnections }}</strong></div>
    </section>

    @if (continuity) {
      <section class="continuity-panel" [attr.aria-label]="'admin.backupRecoveryPosture' | hhTranslate: 'Backup and recovery posture'">
        <div class="continuity-heading">
          <div><h2>{{ "admin.databaseContinuityTitle" | hhTranslate: "Backup and recovery" }}</h2><p>{{ "admin.databaseContinuitySubtitle" | hhTranslate: "PITR, encryption, retention and measured recovery objectives" }}</p></div>
          <span class="status" [class.status-healthy]="continuity.ready">{{ continuity.ready ? ("admin.continuityReady" | hhTranslate: "Ready") : ("admin.continuityNotReady" | hhTranslate: "Not ready") }}</span>
        </div>
        <div class="continuity-grid">
          <div><span>{{ "admin.pitr" | hhTranslate: "PITR" }}</span><strong>{{ continuity.pitrEnabled ? ("common.enabled" | hhTranslate: "Enabled") : ("common.disabled" | hhTranslate: "Disabled") }}</strong></div>
          <div><span>{{ "admin.backupEncryption" | hhTranslate: "Encryption" }}</span><strong>{{ continuity.encryption.provider }}</strong></div>
          <div><span>{{ "admin.retention" | hhTranslate: "Retention" }}</span><strong>{{ continuity.retentionDays }} {{ "common.days" | hhTranslate: "days" }}</strong></div>
          <div><span>{{ "admin.keepLatestBackups" | hhTranslate: "Keep latest per database" }}</span><strong>{{ continuity.keepLastBackupsPerDatabase || 1 }}</strong></div>
          <div><span>{{ "admin.recoveryObjectives" | hhTranslate: "RPO / RTO" }}</span><strong>{{ continuity.targetRpoMinutes }}m / {{ continuity.targetRtoMinutes }}m</strong></div>
          <div><span>{{ "admin.vaultTransit" | hhTranslate: "Vault Transit" }}</span><strong [class.job-error]="!continuity.vault?.reachable">{{ continuity.vault?.reachable ? ("common.healthy" | hhTranslate: "Healthy") : ("admin.continuityNotReady" | hhTranslate: "Not ready") }}</strong></div>
          <div><span>{{ "admin.lastBackup" | hhTranslate: "Last successful backup" }}</span><strong>{{ continuity.lastSuccessfulBackupAt ? (continuity.lastSuccessfulBackupAt | date: "medium") : ("admin.never" | hhTranslate: "Never") }}</strong></div>
          <div><span>{{ "admin.lastRestoreDrill" | hhTranslate: "Last successful restore drill" }}</span><strong>{{ continuity.lastSuccessfulRestoreDrillAt ? (continuity.lastSuccessfulRestoreDrillAt | date: "medium") : ("admin.never" | hhTranslate: "Never") }}</strong></div>
          <div><span>{{ "admin.schedule" | hhTranslate: "Schedule" }}</span><strong>{{ continuity.schedulerEnabled ? continuity.backupIntervalHours + "h / " + continuity.restoreDrillIntervalHours + "h" : ("common.disabled" | hhTranslate: "Disabled") }}</strong></div>
          <div><span>{{ "admin.retryLimit" | hhTranslate: "Retry limit" }}</span><strong>{{ continuity.maxAttempts || 3 }}</strong></div>
        </div>
        @if (continuity.alerts?.length) { <div class="continuity-alerts" role="alert"><mat-icon>warning</mat-icon><div><strong>{{ "admin.continuityAlerts" | hhTranslate: "Continuity alerts" }}</strong><span>{{ continuity.alerts?.join(", ") }}</span></div></div> }
        @if (continuity.configurationErrors.length) { <ul class="configuration-errors">@for (configurationError of continuity.configurationErrors; track configurationError) { <li>{{ configurationError }}</li> }</ul> }
        <div class="continuity-actions">
          <button *ngIf="canMutate" mat-stroked-button type="button" [disabled]="actionLoading || !continuity.ready" (click)="backupRequested.emit()"><mat-icon>backup</mat-icon>{{ "admin.requestBackup" | hhTranslate: "Request backup" }}</button>
          <button *ngIf="canMutate" mat-stroked-button type="button" [disabled]="actionLoading || !continuity.ready" (click)="restoreDrillRequested.emit()"><mat-icon>restore</mat-icon>{{ "admin.restoreDrill" | hhTranslate: "Run restore drill" }}</button>
        </div>
        @if (continuity.latestJob || lastJob) { <p class="job-status">{{ "admin.latestContinuityJob" | hhTranslate: "Latest job" }}: <strong>{{ (lastJob || continuity.latestJob)?.operation }}</strong><span>{{ (lastJob || continuity.latestJob)?.status }}</span>@if ((lastJob || continuity.latestJob)?.errorCode) { <span class="job-error">{{ (lastJob || continuity.latestJob)?.errorCode }}</span> }</p> }
        @if (actionError) { <p class="action-error">{{ actionError }}</p> }
        <div class="audit-table" [attr.aria-label]="'admin.backupRestoreAuditHistory' | hhTranslate: 'Backup and restore audit history'"><div class="audit-heading"><div><h3>{{ "admin.continuityAuditTitle" | hhTranslate: "Backup / restore audit" }}</h3><p>{{ "admin.continuityAuditSubtitle" | hhTranslate: "Durable history of continuity jobs" }}</p></div><button mat-stroked-button type="button" (click)="auditRefreshRequested.emit()" [disabled]="auditLoading"><mat-icon>refresh</mat-icon>{{ "common.refresh" | hhTranslate: "Refresh" }}</button></div>
          @if (auditLoading) { <p class="audit-empty">{{ "admin.loadingAudit" | hhTranslate: "Loading audit history..." }}</p> } @else if (auditEntries.length === 0) { <p class="audit-empty">{{ "admin.noContinuityAudit" | hhTranslate: "No backup or restore jobs recorded yet." }}</p> } @else { <div class="audit-row audit-row-header"><span>{{ "admin.operation" | hhTranslate: "Operation" }}</span><span>{{ "admin.status" | hhTranslate: "Status" }}</span><span>{{ "admin.target" | hhTranslate: "Target" }}</span><span>{{ "admin.updated" | hhTranslate: "Updated" }}</span><span>{{ "admin.actor" | hhTranslate: "Actor" }}</span></div>@for (entry of auditEntries; track entry.jobId) { <div class="audit-row"><strong>{{ entry.operation }}</strong><span [class.audit-complete]="entry.status === 'Completed'" [class.audit-failed]="entry.status === 'Failed'">{{ entry.status }}</span><span>{{ entry.targetEnvironment }}</span><small>{{ entry.updatedAt | date: "medium" }}</small><small>{{ entry.actorSubject || "system" }}</small>@if (entry.errorCode) { <em>{{ entry.errorCode }}</em> }</div> } }
        </div>
      </section>
    }

    <section class="resource-grid" [attr.aria-label]="'admin.databaseResources' | hhTranslate: 'Database resources'">
      @for (database of databases; track database.name) { <article class="resource-card"><div class="resource-heading"><mat-icon>storage</mat-icon><div><h2>{{ database.displayName || database.name }}</h2><p>{{ database.engine || "PostgreSQL" }}</p></div><span class="status" [class.status-healthy]="isHealthy(database)">{{ database.healthStatus || database.status || "Unknown" }}</span></div><dl><div><dt>{{ "admin.databaseSize" | hhTranslate: "Size" }}</dt><dd>{{ database.sizeMb ?? 0 | number: "1.0-1" }} MB</dd></div><div><dt>{{ "admin.databaseConnections" | hhTranslate: "Connections" }}</dt><dd>{{ database.connections ?? 0 }}</dd></div></dl>@if (database.healthChecks?.length) { <ul class="checks">@for (check of database.healthChecks; track check.name) { <li><mat-icon [class.check-ok]="check.status === 'Healthy'">{{ check.status === "Healthy" ? "check_circle" : "error" }}</mat-icon>{{ check.name }}</li> }</ul> }</article> }
    </section>
  `,
  styles: [":host{display:block}.platform-summary{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px;margin-bottom:24px}.summary-card,.resource-card{border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-white);padding:var(--space-5)}.resource-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px}.resource-heading,.continuity-heading,.audit-heading{display:flex;align-items:flex-start;justify-content:space-between;gap:16px}.resource-heading{align-items:center}.resource-heading h2,.continuity-heading h2{margin:0}.resource-heading p,.continuity-heading p{margin:4px 0 0;color:var(--text-secondary)}.status{white-space:nowrap}.status-healthy,.check-ok,.audit-complete{color:var(--color-success)}.job-error,.audit-failed,.action-error{color:var(--color-danger)}.continuity-panel{border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-white);padding:var(--space-5);margin-bottom:24px}.continuity-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:16px;margin:24px 0}.continuity-grid div{display:grid;gap:4px}.continuity-grid span,.summary-label{color:var(--text-secondary)}.continuity-alerts{display:flex;gap:8px;padding:12px;background:var(--surface-muted)}.continuity-alerts div{display:grid;gap:4px}.continuity-actions{display:flex;gap:12px;margin:16px 0}.audit-table{border-top:1px solid var(--border-subtle);padding-top:16px}.audit-row{display:grid;grid-template-columns:1.2fr 1fr 1fr 1.3fr 1fr;gap:12px;padding:10px 0;border-bottom:1px solid var(--border-subtle)}.audit-row-header{font-weight:600}.audit-heading{margin-bottom:12px}.audit-heading h3{margin:0}.audit-heading p{margin:4px 0;color:var(--text-secondary)}.audit-empty{color:var(--text-secondary)}.configuration-errors{color:var(--color-danger)}.checks{padding:0;list-style:none}.checks li{display:flex;align-items:center;gap:8px;margin-top:8px}@media(max-width:800px){.platform-summary{grid-template-columns:1fr}.audit-row{grid-template-columns:1fr 1fr}.continuity-actions{flex-wrap:wrap}}"],
})
export class DatabasePlatformOverviewComponent {
  @Input() databases: PlatformResource[] = [];
  @Input() continuity: DatabaseContinuityStatus | null = null;
  @Input() lastJob: DatabaseContinuityJob | null = null;
  @Input() canMutate = false;
  @Input() actionLoading = false;
  @Input() actionError: string | null = null;
  @Input() auditEntries: DatabaseContinuityAuditEntry[] = [];
  @Input() auditLoading = false;
  @Output() backupRequested = new EventEmitter<void>();
  @Output() restoreDrillRequested = new EventEmitter<void>();
  @Output() auditRefreshRequested = new EventEmitter<void>();

  get healthyCount(): number {
    return this.databases.filter((database) => this.isHealthy(database)).length;
  }

  get totalConnections(): number {
    return this.databases.reduce((total, database) => total + (database.connections ?? 0), 0);
  }

  isHealthy(resource: PlatformResource): boolean {
    return (resource.healthStatus || resource.status || "").toLowerCase() === "healthy";
  }
}
