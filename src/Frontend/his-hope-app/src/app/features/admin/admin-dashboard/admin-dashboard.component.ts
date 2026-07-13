import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-dashboard',
  template: `
    <div class="admin">
      <h1>Administration</h1>
      <div class="admin-grid">
        <mat-card>
          <mat-card-header><mat-card-title>User Management</mat-card-title></mat-card-header>
          <mat-card-content><p>Manage providers, nurses, and staff accounts.</p></mat-card-content>
          <mat-card-actions><button mat-button>Manage Users</button></mat-card-actions>
        </mat-card>
        <mat-card>
          <mat-card-header><mat-card-title>Roles & Permissions</mat-card-title></mat-card-header>
          <mat-card-content><p>Configure RBAC roles and access control.</p></mat-card-content>
          <mat-card-actions><button mat-button>Manage Roles</button></mat-card-actions>
        </mat-card>
        <mat-card>
          <mat-card-header><mat-card-title>System Settings</mat-card-title></mat-card-header>
          <mat-card-content><p>Configure system-wide parameters and defaults.</p></mat-card-content>
          <mat-card-actions><button mat-button>Settings</button></mat-card-actions>
        </mat-card>
        <mat-card>
          <mat-card-header><mat-card-title>Audit Log</mat-card-title></mat-card-header>
          <mat-card-content><p>View system access and activity audit trails.</p></mat-card-content>
          <mat-card-actions><button mat-button>View Logs</button></mat-card-actions>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`
    .admin { padding: 24px; }
    .admin-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px; margin-top: 24px; }
  `],
})
export class AdminDashboardComponent {}
