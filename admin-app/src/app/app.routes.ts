import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { ClientsPageComponent } from './features/clients/clients-page.component';
import { UsersPageComponent } from './features/users/users-page.component';
import { RolesPageComponent } from './features/roles/roles-page.component';
import { ConsentsPageComponent } from './features/consents/consents-page.component';
import { DashboardPageComponent } from './features/dashboard/dashboard-page.component';
import { LoginComponent } from './features/auth/login.component';
import { CallbackComponent } from './features/auth/callback.component';
import { SilentRefreshComponent } from './features/auth/silent-refresh.component';

export const routes: Routes = [
  { path: '', redirectTo: '/clients', pathMatch: 'full' },
  { path: 'clients', component: ClientsPageComponent, canActivate: [authGuard] },
  { path: 'users', component: UsersPageComponent, canActivate: [authGuard] },
  { path: 'roles', component: RolesPageComponent, canActivate: [authGuard] },
  { path: 'consents', component: ConsentsPageComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardPageComponent, canActivate: [authGuard] },
  { path: 'auth/login', component: LoginComponent },
  { path: 'auth/callback', component: CallbackComponent },
  { path: 'auth/silent-refresh', component: SilentRefreshComponent },
  { path: '**', redirectTo: '/clients' },
];
