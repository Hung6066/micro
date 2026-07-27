import { Routes } from '@angular/router';
import { MobileShellComponent } from './mobile-shell.component';
import { MobileLoginComponent } from './mobile-login.component';
import { MobileCallbackComponent } from './mobile-callback.component';
import { mobileAuthGuard } from './core/auth.guard';
import { MobileDashboardComponent } from './features/mobile-dashboard.component';
import { MobileResourcePageComponent } from './features/mobile-resource-page.component';

export const routes: Routes = [
  { path: '', redirectTo: 'admin/dashboard', pathMatch: 'full' },
  { path: 'admin', component: MobileShellComponent, canActivate: [mobileAuthGuard], children: [
    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    { path: 'dashboard', component: MobileDashboardComponent },
    { path: 'clients', component: MobileResourcePageComponent, data: { resource: 'clients' } },
    { path: 'users', component: MobileResourcePageComponent, data: { resource: 'users' } },
    { path: 'roles', component: MobileResourcePageComponent, data: { resource: 'roles' } },
    { path: 'consents', component: MobileResourcePageComponent, data: { resource: 'consents' } },
  ] },
  { path: 'auth/login', component: MobileLoginComponent },
  { path: 'auth/callback', component: MobileCallbackComponent },
  { path: '**', redirectTo: '' },
];
