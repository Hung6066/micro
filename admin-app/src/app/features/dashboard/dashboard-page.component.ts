import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { AdminApiService, DashboardStats } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';
import { HisHopeI18nService, HisHopeMetricCardComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, MatButtonModule, HisHopeMetricCardComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTranslatePipe],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.dashboardTitle' | hhTranslate" [subtitle]="'admin.dashboardSubtitle' | hhTranslate" />
      @if (loading) {
        <hh-state kind="loading" [message]="'admin.loadingDashboard' | hhTranslate" />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <button mat-stroked-button type="button" (click)="loadDashboardStats()">{{ 'common.retry' | hhTranslate }}</button>
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card icon="vpn_key" [label]="'admin.clients' | hhTranslate" [value]="stats.totalClients" link="/clients" [action]="'admin.manageClients' | hhTranslate" />
          <hh-metric-card icon="people" [label]="'admin.users' | hhTranslate" [value]="stats.totalUsers" link="/users" [action]="'admin.manageUsers' | hhTranslate" />
          <hh-metric-card icon="badge" [label]="'admin.roles' | hhTranslate" [value]="stats.totalRoles" link="/roles" [action]="'admin.manageRoles' | hhTranslate" tone="info" />
          <hh-metric-card icon="checklist" [label]="'admin.consents' | hhTranslate" [value]="stats.totalConsents" link="/consents" [action]="'admin.reviewConsents' | hhTranslate" tone="warning" />
        </div>
      }
    </hh-page-layout>
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px; }
    @media (max-width: 900px) { .stats-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 600px) { .stats-grid { grid-template-columns: 1fr; } }
  `],
})
export class DashboardPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly i18n = inject(HisHopeI18nService);
  stats: DashboardStats | null = null;
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.loadDashboardStats(); }

  loadDashboardStats(): void {
    this.loading = true;
    this.error = null;
    this.api.getDashboardStats().pipe(
      finalize(() => this.loading = false),
      catchError(() => {
        this.error = this.i18n.t('admin.dashboardLoadFailed');
        return of(null);
      }),
    ).subscribe(stats => this.stats = stats);
  }
}
