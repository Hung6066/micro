import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";

export const routes: Routes = [
  { path: "", redirectTo: "/clients", pathMatch: "full" },
  {
    path: "clients",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./features/clients/clients-page.component").then(
        (m) => m.ClientsPageComponent,
      ),
  },
  {
    path: "users",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./features/users/users-page.component").then(
        (m) => m.UsersPageComponent,
      ),
  },
  {
    path: "roles",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./features/roles/roles-page.component").then(
        (m) => m.RolesPageComponent,
      ),
  },
  {
    path: "consents",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./features/consents/consents-page.component").then(
        (m) => m.ConsentsPageComponent,
      ),
  },
  {
    path: "dashboard",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./features/dashboard/dashboard-page.component").then(
        (m) => m.DashboardPageComponent,
      ),
  },
  {
    path: "auth/login",
    loadComponent: () =>
      import("./features/auth/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "auth/callback",
    loadComponent: () =>
      import("./features/auth/callback.component").then(
        (m) => m.CallbackComponent,
      ),
  },
  {
    path: "auth/silent-refresh",
    loadComponent: () =>
      import("./features/auth/silent-refresh.component").then(
        (m) => m.SilentRefreshComponent,
      ),
  },
  { path: "**", redirectTo: "/clients" },
];
