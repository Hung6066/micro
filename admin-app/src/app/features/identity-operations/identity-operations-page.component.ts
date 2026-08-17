import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HisHopeI18nService, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopePermissionService, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { AdminApiService, AdminSession, BulkImportPreview, BulkImportResult } from '../../core/services/admin-api.service';

@Component({
  selector: 'app-identity-operations-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSnackBarModule, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.identityOperations' | hhTranslate:'Identity operations'" [subtitle]="'admin.identityOperationsSubtitle' | hhTranslate:'Incident response, lifecycle import and outbox operations'" />
      <p class="notice">{{ 'admin.identityOperationsNotice' | hhTranslate:'All actions are audited. Secrets and credential material remain server-side.' }}</p>
      <section class="grid two-col">
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.sessionControls' | hhTranslate:'Session controls' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.userId' | hhTranslate:'User ID' }}</mat-label><input matInput [(ngModel)]="userId" autocomplete="off"></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.reason' | hhTranslate:'Reason' }}</mat-label><textarea matInput rows="2" [(ngModel)]="reason"></textarea></mat-form-field>
          <div class="actions"><button mat-stroked-button type="button" (click)="loadSessions()" [disabled]="busy || !userId || !can('admin.sessions.read')">{{ 'admin.loadSessions' | hhTranslate:'Load sessions' }}</button><button mat-flat-button color="warn" type="button" (click)="revokeAllSessions()" [disabled]="busy || !userId || !reason || !can('admin.sessions.revoke')">{{ 'admin.revokeAllSessions' | hhTranslate:'Revoke all sessions' }}</button></div>
          <div class="table-wrap" *ngIf="sessions.length"><table><thead><tr><th>ID</th><th>{{ 'admin.expires' | hhTranslate:'Expires' }}</th><th>{{ 'admin.status' | hhTranslate:'Status' }}</th><th></th></tr></thead><tbody><tr *ngFor="let session of sessions"><td class="mono">{{ session.id }}</td><td>{{ session.expiresAt | date:'short' }}</td><td>{{ session.active ? ('admin.active' | hhTranslate:'active') : ('admin.expired' | hhTranslate:'expired') }}</td><td><button mat-button color="warn" (click)="revokeSession(session)" [disabled]="busy || !can('admin.sessions.revoke')">{{ 'admin.revoke' | hhTranslate:'Revoke' }}</button></td></tr></tbody></table></div>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.credentialReset' | hhTranslate:'Credential reset' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <p class="muted">{{ 'admin.credentialResetNotice' | hhTranslate:'Reset invalidates existing tokens and requires the user to authenticate again.' }}</p><mat-checkbox [(ngModel)]="resetMfa">{{ 'admin.resetMfa' | hhTranslate:'Reset MFA' }}</mat-checkbox><mat-checkbox [(ngModel)]="revokePasskeys">{{ 'admin.revokePasskeys' | hhTranslate:'Revoke passkeys' }}</mat-checkbox><button mat-flat-button color="warn" type="button" (click)="resetCredentials()" [disabled]="busy || !userId || !reason || (!resetMfa && !revokePasskeys) || !can('admin.credentials.reset')">{{ 'admin.resetCredentials' | hhTranslate:'Reset selected credentials' }}</button>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.bulkImport' | hhTranslate:'Bulk user import' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <input type="file" accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" (change)="selectFile($event)"><p class="muted">{{ 'admin.bulkImportLimit' | hhTranslate:'CSV/XLSX, maximum 10 MB and 10,000 users.' }}</p><div class="actions"><button mat-stroked-button type="button" (click)="previewImport()" [disabled]="busy || !file || !can('admin.users.read')">{{ 'admin.previewImport' | hhTranslate:'Preview' }}</button><button mat-flat-button color="primary" type="button" (click)="executeImport()" [disabled]="busy || !file || !can('admin.users.write')">{{ 'admin.executeImport' | hhTranslate:'Execute import' }}</button></div><p *ngIf="preview">{{ preview.valid }}/{{ preview.total }} {{ 'admin.validRows' | hhTranslate:'valid rows' }}</p><p *ngIf="importResult">{{ importResult.created }} {{ 'admin.created' | hhTranslate:'created' }}, {{ importResult.skipped }} {{ 'admin.skipped' | hhTranslate:'skipped' }}, {{ importResult.failed }} {{ 'admin.failed' | hhTranslate:'failed' }}</p>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.outboxOperations' | hhTranslate:'Outbox operations' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.provisioningTarget' | hhTranslate:'Provisioning target' }}</mat-label><mat-select [(ngModel)]="provisioningTarget"><mat-option value="scim">SCIM</mat-option><mat-option value="entra">Microsoft Entra ID</mat-option><mat-option value="google-workspace">Google Workspace</mat-option></mat-select></mat-form-field><button mat-stroked-button type="button" (click)="reconcile()" [disabled]="busy || !can('admin.provisioning.manage')">{{ 'admin.reconcile' | hhTranslate:'Queue full reconcile' }}</button><mat-form-field appearance="outline"><mat-label>{{ 'admin.ssfOutboxId' | hhTranslate:'SSF outbox ID' }}</mat-label><input matInput [(ngModel)]="ssfId"></mat-form-field><button mat-stroked-button type="button" (click)="retrySsf()" [disabled]="busy || !ssfId || !can('admin.security-signals.manage')">{{ 'admin.retrySsf' | hhTranslate:'Retry SSF delivery' }}</button>
        </mat-card-content></mat-card>
      </section><p class="error" *ngIf="error">{{ error }}</p>
    </hh-page-layout>
  `,
  styles: [`:host{display:block}.grid{display:grid;gap:var(--space-4);margin-top:var(--space-4)}.two-col{grid-template-columns:repeat(2,minmax(0,1fr))}.form-grid{display:grid;gap:var(--space-3)}.actions{display:flex;flex-wrap:wrap;gap:var(--space-2)}.notice,.muted{color:var(--text-secondary);font-size:var(--font-size-caption)}.notice{padding:12px;border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;font-size:var(--font-size-caption)}th,td{text-align:left;padding:8px;border-bottom:1px solid var(--border-subtle);white-space:nowrap}.mono{font-family:var(--font-mono);font-size:11px}.error{color:var(--color-danger)}@media(max-width:800px){.two-col{grid-template-columns:1fr}}`],
})
export class IdentityOperationsPageComponent {
  private readonly api = inject(AdminApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly snack = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  userId = '';
  reason = '';
  resetMfa = true;
  revokePasskeys = true;
  sessions: AdminSession[] = [];
  file?: File;
  preview?: BulkImportPreview;
  importResult?: BulkImportResult;
  provisioningTarget: 'scim' | 'entra' | 'google-workspace' = 'scim';
  ssfId = '';
  busy = false;
  error = '';

  can(permission: string): boolean { return this.permissions.has(permission); }

  loadSessions(): void { if (!this.can('admin.sessions.read')) return; this.run(() => this.api.getAdminSessions(this.userId), result => { this.sessions = result.sessions; }); }
  revokeSession(session: AdminSession): void { if (!this.can('admin.sessions.revoke')) return; this.run(() => this.api.revokeAdminSession(this.userId, session.id, this.reason), () => { this.sessions = this.sessions.filter(item => item.id !== session.id); }); }
  revokeAllSessions(): void { if (!this.can('admin.sessions.revoke')) return; this.run(() => this.api.revokeAllAdminSessions(this.userId, this.reason), () => { this.sessions = []; }); }
  resetCredentials(): void { if (!this.can('admin.credentials.reset')) return; this.run(() => this.api.resetAdminCredentials(this.userId, { resetMfa: this.resetMfa, revokePasskeys: this.revokePasskeys, reason: this.reason }), () => this.notify('admin.credentialsReset')); }
  selectFile(event: Event): void { this.file = (event.target as HTMLInputElement).files?.[0]; this.preview = undefined; this.importResult = undefined; }
  previewImport(): void { if (!this.file || !this.can('admin.users.read')) return; this.run(() => this.api.previewUserImport(this.file!), result => { this.preview = result as BulkImportPreview; }); }
  executeImport(): void { if (!this.file || !this.can('admin.users.write')) return; this.run(() => this.api.importUsers(this.file!), result => { this.importResult = result; this.notify('admin.importCompleted'); }); }
  reconcile(): void { if (!this.can('admin.provisioning.manage')) return; this.run(() => this.api.reconcileProvisioning(this.provisioningTarget), result => this.notify(`admin.reconcileQueued:${result.queued}`)); }
  retrySsf(): void { if (!this.can('admin.security-signals.manage')) return; this.run(() => this.api.retrySecuritySignal(this.ssfId), () => { this.ssfId = ''; this.notify('admin.ssfRetryQueued'); }); }

  private run<T>(request: () => import('rxjs').Observable<T>, onSuccess: (value: T) => void): void {
    this.busy = true; this.error = '';
    request().subscribe({ next: value => onSuccess(value), error: () => { this.error = this.i18n.t('admin.identityOperationFailed', 'Identity operation was rejected.'); this.busy = false; this.cdr.markForCheck(); }, complete: () => { this.busy = false; this.cdr.markForCheck(); } });
  }
  private notify(key: string): void { this.snack.open(this.i18n.t(key, 'Operation queued'), this.i18n.t('admin.close', 'Close'), { duration: 3000 }); }
}
