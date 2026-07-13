import { Component } from '@angular/core';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `
    <div class="dashboard">
      <h1>Welcome, {{ currentUser?.fullName }}</h1>
      <p class="subtitle">Dashboard overview</p>

      <div class="stats-grid">
        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon>people</mat-icon>
            <div class="stat-info">
              <span class="stat-value">--</span>
              <span class="stat-label">Total Patients</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon>calendar_today</mat-icon>
            <div class="stat-info">
              <span class="stat-value">--</span>
              <span class="stat-label">Today's Appointments</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon>emergency</mat-icon>
            <div class="stat-info">
              <span class="stat-value">--</span>
              <span class="stat-label">Active Encounters</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-content>
            <mat-icon>assignment</mat-icon>
            <div class="stat-info">
              <span class="stat-value">--</span>
              <span class="stat-label">Pending Lab Results</span>
            </div>
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`
    .dashboard { padding: 24px; }
    .subtitle { color: #666; margin-bottom: 32px; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 20px; }
    .stat-card mat-card-content { display: flex; align-items: center; gap: 16px; padding: 20px; }
    .stat-card mat-icon { font-size: 40px; width: 40px; height: 40px; color: #5c6bc0; }
    .stat-info { display: flex; flex-direction: column; }
    .stat-value { font-size: 28px; font-weight: 500; }
    .stat-label { color: #666; font-size: 14px; }
  `],
})
export class DashboardComponent {
  currentUser = this.authService.getCurrentUser();
  constructor(private authService: AuthService) {}
}
