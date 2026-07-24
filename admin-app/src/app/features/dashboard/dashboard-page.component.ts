import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AdminApiService, DashboardStats } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">Admin Dashboard</h1>
    </div>
    @if (loading) {
      <div class="loading-state"><mat-spinner diameter="32"></mat-spinner></div>
    } @else if (stats) {
      <div class="stats-grid">
        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon class="stat-icon">vpn_key</mat-icon>
            <div class="stat-value">{{ stats.totalClients }}</div>
            <div class="stat-label">Clients</div>
          </mat-card-content>
        </mat-card>
        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon class="stat-icon">people</mat-icon>
            <div class="stat-value">{{ stats.totalUsers }}</div>
            <div class="stat-label">Users</div>
          </mat-card-content>
        </mat-card>
        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon class="stat-icon">badge</mat-icon>
            <div class="stat-value">{{ stats.totalRoles }}</div>
            <div class="stat-label">Roles</div>
          </mat-card-content>
        </mat-card>
        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon class="stat-icon">checklist</mat-icon>
            <div class="stat-value">{{ stats.totalConsents }}</div>
            <div class="stat-label">Consents</div>
          </mat-card-content>
        </mat-card>
      </div>
    }
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 20px; }
    .stat-card { text-align: center; padding: 8px; }
    .stat-icon { font-size: 40px; width: 40px; height: 40px; margin-bottom: 12px; color: #3F51B5; }
    .stat-value { font-size: 36px; font-weight: 700; line-height: 1.2; color: #1A1A1A; }
    .stat-label { font-size: 14px; color: #A1A09B; margin-top: 4px; }
  `],
})
export class DashboardPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  stats: DashboardStats | null = null;
  loading = false;

  ngOnInit(): void {
    this.loading = true;
    this.api.getDashboardStats().pipe(
      finalize(() => this.loading = false),
      catchError(() => of(null)),
    ).subscribe(stats => this.stats = stats);
  }
}
