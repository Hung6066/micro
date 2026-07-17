// @ts-nocheck
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { SharedModule } from '@shared/shared.module';
import { AdminDashboardComponent } from './admin-dashboard/admin-dashboard.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';
import { SettingsComponent } from './settings/settings.component';
import { AuditLogsComponent } from './audit-logs/audit-logs.component';

const routes: Routes = [
  { path: '', component: AdminDashboardComponent },
  { path: 'manage-users', component: ManageUsersComponent },
  { path: 'manage-roles', component: ManageRolesComponent },
  { path: 'settings', component: SettingsComponent },
  { path: 'audit-logs', component: AuditLogsComponent },
];

@NgModule({
  declarations: [
    AdminDashboardComponent,
    ManageUsersComponent,
    ManageRolesComponent,
    SettingsComponent,
    AuditLogsComponent,
  ],
  imports: [
    SharedModule,
    RouterModule.forChild(routes),
    MatDatepickerModule,
    MatNativeDateModule,
    MatExpansionModule,
    MatSlideToggleModule,
  ],
})
export class AdminModule {}
