import { Routes } from "@angular/router";
import { authGuard } from "./core/guards/auth.guard";
import { endUserPortalGuard } from "./core/guards/end-user-portal.guard";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "home" },
  {
    path: "home",
    loadComponent: () =>
      import("./features/home/home-page.component").then((m) => m.HomePageComponent),
  },
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
    path: "catalog",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/catalog/catalog-page.component").then((m) => m.CatalogPageComponent),
  },
  {
    path: "cart",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/cart/cart-page.component").then((m) => m.CartPageComponent),
  },
  {
    path: "orders",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/orders/orders-page.component").then((m) => m.OrdersPageComponent),
  },
  {
    path: "profile",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/profile/profile-page.component").then((m) => m.ProfilePageComponent),
  },
  {
    path: "notifications",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/notifications/notifications-page.component").then(
        (m) => m.NotificationsPageComponent,
      ),
  },
  { path: "**", redirectTo: "home" },
];
