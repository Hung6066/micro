import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { ResourcesPageComponent } from './features/resources/resources-page.component';
import { LogsPageComponent } from './features/logs/logs-page.component';
import { TracesPageComponent } from './features/traces/traces-page.component';
import { TraceDetailComponent } from './features/traces/trace-detail.component';
import { MetricsPageComponent } from './features/metrics/metrics-page.component';
import { LoginComponent } from './features/auth/login.component';
import { SloPageComponent } from './features/slo/slo-page.component';

export const routes: Routes = [
  { path: '', redirectTo: '/resources', pathMatch: 'full' },
  { path: 'resources', component: ResourcesPageComponent, canActivate: [authGuard] },
  { path: 'logs', component: LogsPageComponent, canActivate: [authGuard] },
  { path: 'traces', component: TracesPageComponent, canActivate: [authGuard] },
  { path: 'traces/:traceId', component: TraceDetailComponent, canActivate: [authGuard] },
  { path: 'metrics', component: MetricsPageComponent, canActivate: [authGuard] },
  { path: 'slo', component: SloPageComponent, canActivate: [authGuard] },
  { path: 'auth/login', component: LoginComponent },
  { path: '**', redirectTo: '/resources' },
];
