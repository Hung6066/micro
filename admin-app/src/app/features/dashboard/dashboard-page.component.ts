import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { AdminApiService, DashboardStats } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';
import { HisHopeMetricCardComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent } from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, MatButtonModule, HisHopeMetricCardComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader title="Admin Dashboard" subtitle="Identity administration overview" />
      @if (loading) {
        <hh-state kind="loading" message="Loading dashboard..." />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <button mat-stroked-button type="button" (click)="loadDashboardStats()">Retry</button>
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card icon="vpn_key" label="Clients" [value]="stats.totalClients" link="/clients" action="Manage clients" />
          <hh-metric-card icon="people" label="Users" [value]="stats.totalUsers" link="/users" action="Manage users" />
          <hh-metric-card icon="badge" label="Roles" [value]="stats.totalRoles" link="/roles" action="Manage roles" tone="info" />
          <hh-metric-card icon="checklist" label="Consents" [value]="stats.totalConsents" link="/consents" action="Review consents" tone="warning" />
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
        this.error = 'Failed to load dashboard data.';
        return of(null);
      }),
    ).subscribe(stats => this.stats = stats);
  }
}
