import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { AdminApiService, MobileDeliverySummary, MobileDeviceRegistration } from '../../core/services/admin-api.service';
import { HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopePermissionService, HisHopeStateComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { catchError, finalize, forkJoin, of } from 'rxjs';

@Component({
  selector: 'app-mobile-operations-page',
  standalone: true,
  imports: [CommonModule, MatButtonModule, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.mobileOperations' | hhTranslate" [subtitle]="'admin.mobileOperationsSubtitle' | hhTranslate" />
      @if (loading) {
        <hh-state kind="loading" [message]="'admin.loadingMobileOperations' | hhTranslate" />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error | hhTranslate"><button mat-stroked-button type="button" (click)="load()">{{ 'common.retry' | hhTranslate }}</button></hh-state>
      } @else {
        <section class="summary-grid" [attr.aria-label]="'admin.mobileDeliverySummary' | hhTranslate:'Mobile delivery summary'">
          <article><strong>{{ summary?.queued ?? 0 }}</strong><span>{{ 'admin.mobileQueued' | hhTranslate }}</span></article>
          <article><strong>{{ summary?.sent ?? 0 }}</strong><span>{{ 'admin.mobileSent' | hhTranslate }}</span></article>
          <article><strong>{{ summary?.failed ?? 0 }}</strong><span>{{ 'admin.mobileFailed' | hhTranslate }}</span></article>
          <article><strong>{{ devices.length }}</strong><span>{{ 'admin.mobileDevices' | hhTranslate }}</span></article>
        </section>
        <section class="panel">
          <div class="panel__header"><h2>{{ 'admin.mobileDevices' | hhTranslate }}</h2><button mat-stroked-button type="button" (click)="load()">{{ 'common.refresh' | hhTranslate }}</button></div>
          @if (!devices.length) { <hh-state kind="empty" [message]="'admin.noMobileDevices' | hhTranslate" /> }
          @else {
            <div class="table-wrap"><table><thead><tr><th>{{ 'admin.platform' | hhTranslate }}</th><th>{{ 'admin.userId' | hhTranslate }}</th><th>{{ 'admin.lastSeen' | hhTranslate }}</th><th>{{ 'admin.status' | hhTranslate }}</th><th></th></tr></thead><tbody>
              @for (device of devices; track device.id) { <tr><td>{{ device.platform }}</td><td class="mono">{{ device.userId }}</td><td>{{ device.lastSeenAt | date:'short' }}</td><td>{{ device.active ? ('admin.active' | hhTranslate) : ('admin.revoked' | hhTranslate) }}</td><td>@if (device.active && canWrite) { <button mat-button color="warn" type="button" (click)="revoke(device)">{{ 'admin.revoke' | hhTranslate }}</button> }</td></tr> }
            </tbody></table></div>
          }
        </section>
        @if (summary?.lastFailure; as failure) { <p class="failure">{{ 'admin.lastMobileFailure' | hhTranslate }}: {{ failure.platform }} · {{ failure.createdAt | date:'short' }}</p> }
      }
    </hh-page-layout>
  `,
  styles: [`
    .summary-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:16px; margin-bottom:16px; }
    .summary-grid article,.panel { border:1px solid var(--border-default); border-radius:var(--radius-card); background:var(--surface-white); }
    .summary-grid article { display:grid; gap:5px; padding:18px; } .summary-grid strong { font-size:28px; } .summary-grid span { color:var(--text-secondary); }
    .panel { padding:18px; } .panel__header { display:flex; justify-content:space-between; align-items:center; gap:12px; } h2 { margin:0; font-size:18px; }
    .table-wrap { overflow:auto; } table { width:100%; border-collapse:collapse; } th,td { padding:12px 8px; border-bottom:1px solid var(--border-light); text-align:left; white-space:nowrap; } th { color:var(--text-secondary); font-size:12px; } .mono { font-family:var(--font-mono); font-size:12px; } .failure { color:var(--color-danger); }
    @media (max-width:800px) { .summary-grid { grid-template-columns:repeat(2,minmax(0,1fr)); } }
  `],
})
export class MobileOperationsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly permissions = inject(HisHopePermissionService);
  get canWrite(): boolean { return this.permissions.has('admin.users.write'); }
  devices: MobileDeviceRegistration[] = [];
  summary: MobileDeliverySummary | null = null;
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.error = null;
    forkJoin({ devices: this.api.getMobileDevices(), summary: this.api.getMobileDeliverySummary() }).pipe(
      finalize(() => { this.loading = false; this.cdr.markForCheck(); }),
      catchError(() => { this.error = 'admin.mobileOperationsLoadFailed'; return of(null); }),
    ).subscribe(result => {
      if (result) { this.devices = result.devices.items; this.summary = result.summary; }
      this.cdr.markForCheck();
    });
  }

  revoke(device: MobileDeviceRegistration): void {
    if (!this.canWrite) return;
    this.api.revokeMobileDevice(device.id).subscribe(() => {
      device.active = false;
      device.revokedAt = new Date().toISOString();
      this.cdr.markForCheck();
    });
  }
}
