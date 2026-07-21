import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { ResourcesPageComponent } from './features/resources/resources-page.component';
import { LogsPageComponent } from './features/logs/logs-page.component';
import { TracesPageComponent } from './features/traces/traces-page.component';
import { MetricsPageComponent } from './features/metrics/metrics-page.component';
import { LoginComponent } from './features/auth/login.component';

export const routes: Routes = [
  { path: '', redirectTo: '/resources', pathMatch: 'full' },
  { path: 'resources', component: ResourcesPageComponent, canActivate: [authGuard] },
  { path: 'logs', component: LogsPageComponent, canActivate: [authGuard] },
  { path: 'traces', component: TracesPageComponent, canActivate: [authGuard] },
  { path: 'metrics', component: MetricsPageComponent, canActivate: [authGuard] },
  { path: 'auth/login', component: LoginComponent },
  { path: 'auth/callback', component: LoginComponent },
  { path: '**', redirectTo: '/resources' },
];
