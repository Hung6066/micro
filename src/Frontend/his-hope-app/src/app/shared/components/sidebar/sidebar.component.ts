import { Component } from '@angular/core';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-sidebar',
  template: `
    <mat-sidenav-container class="sidebar-container">
      <mat-sidenav mode="side" opened class="sidebar">
        <div class="sidebar-header">
          <mat-icon class="logo-icon">local_hospital</mat-icon>
          <span class="logo-text">His.Hope</span>
        </div>

        <mat-nav-list>
          <a mat-list-item routerLink="/dashboard" routerLinkActive="active">
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="/patients" routerLinkActive="active">
            <mat-icon matListItemIcon>people</mat-icon>
            <span matListItemTitle>Patients</span>
          </a>
          <a mat-list-item routerLink="/appointments" routerLinkActive="active">
            <mat-icon matListItemIcon>calendar_today</mat-icon>
            <span matListItemTitle>Appointments</span>
          </a>
          <a mat-list-item routerLink="/clinical" routerLinkActive="active">
            <mat-icon matListItemIcon>medical_services</mat-icon>
            <span matListItemTitle>Clinical</span>
          </a>
          <a mat-list-item routerLink="/admin" routerLinkActive="active">
            <mat-icon matListItemIcon>settings</mat-icon>
            <span matListItemTitle>Admin</span>
          </a>
        </mat-nav-list>

        <div class="sidebar-footer">
          <div class="user-info">
            <span>{{ currentUser?.fullName }}</span>
            <small>{{ currentUser?.specialty }}</small>
          </div>
          <button mat-icon-button (click)="logout()">
            <mat-icon>logout</mat-icon>
          </button>
        </div>
      </mat-sidenav>
      <mat-sidenav-content></mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .sidebar-container { height: 100vh; position: fixed; left: 0; top: 0; width: 260px; }
    .sidebar { width: 260px; background: #1a237e; color: white; display: flex; flex-direction: column; }
    .sidebar-header { display: flex; align-items: center; padding: 20px; gap: 12px; border-bottom: 1px solid rgba(255,255,255,0.1); }
    .logo-icon { font-size: 32px; width: 32px; height: 32px; color: #5c6bc0; }
    .logo-text { font-size: 22px; font-weight: 500; }
    mat-nav-list { flex: 1; padding-top: 8px; }
    mat-nav-list a { color: rgba(255,255,255,0.8); margin: 4px 8px; border-radius: 8px; }
    mat-nav-list a:hover { background: rgba(255,255,255,0.1); color: white; }
    mat-nav-list a.active { background: rgba(92, 107, 192, 0.3); color: white; }
    mat-nav-list ::ng-deep .mat-icon { color: rgba(255,255,255,0.7); }
    .sidebar-footer { border-top: 1px solid rgba(255,255,255,0.1); padding: 12px 16px; display: flex; align-items: center; justify-content: space-between; }
    .user-info { display: flex; flex-direction: column; }
    .user-info small { opacity: 0.7; font-size: 12px; }
  `],
})
export class SidebarComponent {
  currentUser = this.authService.getCurrentUser();

  constructor(private authService: AuthService) {}

  logout(): void {
    this.authService.logout();
    window.location.href = '/auth/login';
  }
}
