import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { dashboardPermissionGuard } from './core/guards/dashboard-permission.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/resources', pathMatch: 'full' },
  {
    path: 'resources',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/resources/resources-page.component').then(m => m.ResourcesPageComponent),
  },
  {
    path: 'logs',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/logs/logs-page.component').then(m => m.LogsPageComponent),
  },
  {
    path: 'traces',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/traces/traces-page.component').then(m => m.TracesPageComponent),
  },
  {
    path: 'traces/:traceId',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/traces/trace-detail.component').then(m => m.TraceDetailComponent),
  },
  {
    path: 'metrics',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/metrics/metrics-page.component').then(m => m.MetricsPageComponent),
  },
  {
    path: 'slo',
    canActivate: [authGuard, dashboardPermissionGuard],
    loadComponent: () => import('./features/slo/slo-page.component').then(m => m.SloPageComponent),
  },
  { path: 'auth/login', loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'auth/callback', loadComponent: () => import('./features/auth/callback.component').then(m => m.CallbackComponent) },
  { path: 'auth/silent-refresh', loadComponent: () => import('./features/auth/silent-refresh.component').then(m => m.SilentRefreshComponent) },
  { path: 'access-denied', loadComponent: () => import('./features/access-denied/access-denied.component').then(m => m.AccessDeniedComponent) },
  { path: '**', redirectTo: '/resources' },
];
