import { Routes } from '@angular/router';
import { AdminDashboardComponent } from './admin-dashboard/admin-dashboard.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';
import { AuditLogsComponent } from './audit-logs/audit-logs.component';
import { SettingsComponent } from './settings/settings.component';

export const ADMIN_ROUTES: Routes = [
  { path: '', component: AdminDashboardComponent },
  { path: 'manage-users', component: ManageUsersComponent },
  { path: 'manage-roles', component: ManageRolesComponent },
  { path: 'audit-logs', component: AuditLogsComponent },
  { path: 'settings', component: SettingsComponent },
];
