import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";
import { operatorPortalGuard } from "./core/guards/operator-portal.guard";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "dashboard" },
  {
    path: "auth/login",
    loadComponent: () =>
      import("./features/auth/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "auth/callback",
    loadComponent: () =>
      import("./features/auth/callback.component").then((m) => m.CallbackComponent),
  },
  {
    path: "dashboard",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/dashboard/dashboard-page.component").then(
        (m) => m.DashboardPageComponent,
      ),
  },
  {
    path: "users",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/users/users-page.component").then((m) => m.UsersPageComponent),
  },
  {
    path: "orders",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/orders/orders-page.component").then((m) => m.OrdersPageComponent),
  },
  { path: "**", redirectTo: "dashboard" },
];
