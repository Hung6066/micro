import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { roleGuard } from '@core/guards/role.guard';
import { permissionGuard } from '@core/guards/permission.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full',
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('@features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: 'access-denied',
    loadComponent: () =>
      import('@shared/pages/access-denied/access-denied.component').then(
        (m) => m.AccessDeniedComponent,
      ),
  },
  {
    path: 'dashboard',
    loadChildren: () =>
      import('@features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
    canActivate: [authGuard],
  },
  {
    path: 'patients',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['patients.view'] },
    loadChildren: () =>
      import('@features/patients/patients.routes').then((m) => m.PATIENT_ROUTES),
  },
  {
    path: 'appointments',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['appointments.view'] },
    loadChildren: () =>
      import('@features/appointments/appointments.routes').then((m) => m.APPOINTMENT_ROUTES),
  },
  {
    path: 'clinical',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['clinical.view'] },
    loadChildren: () =>
      import('@features/clinical/clinical.routes').then((m) => m.CLINICAL_ROUTES),
  },
  {
    path: 'pharmacy',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['pharmacy.view'] },
    loadChildren: () =>
      import('@features/pharmacy/pharmacy.routes').then((m) => m.PHARMACY_ROUTES),
  },
  {
    path: 'lab',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['lab.view'] },
    loadChildren: () =>
      import('@features/lab/lab.routes').then((m) => m.LAB_ROUTES),
  },
  {
    path: 'billing',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['billing.view'] },
    loadChildren: () =>
      import('@features/billing/billing.routes').then(m => m.BILLING_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['admin'] },
    loadChildren: () =>
      import('@features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'reports',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['reports.view'] },
    loadChildren: () =>
      import('@features/reports/reports.routes').then((m) => m.REPORTS_ROUTES),
  },
  {
    path: '**',
    redirectTo: '/dashboard',
  },
];
