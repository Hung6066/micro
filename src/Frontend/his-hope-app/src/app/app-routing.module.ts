import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from '@core/guards/auth.guard';
import { RoleGuard } from '@core/guards/role.guard';
import { PermissionGuard } from '@core/guards/permission.guard';

const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full',
  },
  // ─── Auth (no guards) ───────────────────────────────────────────────
  {
    path: 'auth',
    loadChildren: () =>
      import('@features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  // ─── Access Denied (no guards) ──────────────────────────────────────
  {
    path: 'access-denied',
    loadComponent: () =>
      import('@shared/pages/access-denied/access-denied.component').then(
        (m) => m.AccessDeniedComponent,
      ),
  },
  // ─── Dashboard ──────────────────────────────────────────────────────
  {
    path: 'dashboard',
    loadChildren: () =>
      import('@features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
    canActivate: [AuthGuard],
  },
  // ─── Patients ───────────────────────────────────────────────────────
  {
    path: 'patients',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['patients.view'] },
    loadChildren: () =>
      import('@features/patients/patients.module').then((m) => m.PatientsModule),
  },
  // ─── Appointments ───────────────────────────────────────────────────
  {
    path: 'appointments',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['appointments.view'] },
    loadChildren: () =>
      import('@features/appointments/appointments.module').then((m) => m.AppointmentsModule),
  },
  // ─── Clinical ───────────────────────────────────────────────────────
  {
    path: 'clinical',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['clinical.view'] },
    loadChildren: () =>
      import('@features/clinical/clinical.module').then((m) => m.ClinicalModule),
  },
  // ─── Pharmacy ───────────────────────────────────────────────────────
  {
    path: 'pharmacy',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['pharmacy.view'] },
    loadChildren: () =>
      import('@features/pharmacy/pharmacy.module').then((m) => m.PharmacyModule),
  },
  // ─── Lab ────────────────────────────────────────────────────────────
  {
    path: 'lab',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['lab.view'] },
    loadChildren: () =>
      import('@features/lab/lab.module').then((m) => m.LabModule),
  },
  // ─── Billing ────────────────────────────────────────────────────────
  {
    path: 'billing',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['billing.view'] },
    loadChildren: () =>
      import('@features/billing/billing.module').then((m) => m.BillingModule),
  },
  // ─── Admin (role-based) ─────────────────────────────────────────────
  {
    path: 'admin',
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['admin'] },
    loadChildren: () =>
      import('@features/admin/admin.module').then((m) => m.AdminModule),
  },
  // ─── Reports ────────────────────────────────────────────────────────
  {
    path: 'reports',
    canActivate: [AuthGuard, PermissionGuard],
    data: { permissions: ['reports.view'] },
    loadChildren: () =>
      import('@features/reports/reports.routes').then((m) => m.REPORTS_ROUTES),
  },
  // ─── Wildcard ───────────────────────────────────────────────────────
  {
    path: '**',
    redirectTo: '/dashboard',
  },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
