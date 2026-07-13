import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from '@core/guards/auth.guard';

const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full',
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('@features/auth/auth.module').then((m) => m.AuthModule),
  },
  {
    path: 'dashboard',
    loadChildren: () =>
      import('@features/dashboard/dashboard.module').then((m) => m.DashboardModule),
    canActivate: [AuthGuard],
  },
  {
    path: 'patients',
    loadChildren: () =>
      import('@features/patients/patients.module').then((m) => m.PatientsModule),
    canActivate: [AuthGuard],
  },
  {
    path: 'appointments',
    loadChildren: () =>
      import('@features/appointments/appointments.module').then((m) => m.AppointmentsModule),
    canActivate: [AuthGuard],
  },
  {
    path: 'clinical',
    loadChildren: () =>
      import('@features/clinical/clinical.module').then((m) => m.ClinicalModule),
    canActivate: [AuthGuard],
  },
  {
    path: 'admin',
    loadChildren: () =>
      import('@features/admin/admin.module').then((m) => m.AdminModule),
    canActivate: [AuthGuard],
  },
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
