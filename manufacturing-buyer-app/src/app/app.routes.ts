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
    path: "catalog/:productId",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/catalog/product-detail-page.component").then(
        (m) => m.ProductDetailPageComponent,
      ),
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
    path: "orders/:orderId",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/orders/order-detail-page.component").then(
        (m) => m.OrderDetailPageComponent,
      ),
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
  {
    path: "blog",
    loadComponent: () =>
      import("./features/blog/blog-list-page.component").then((m) => m.BlogListPageComponent),
  },
  {
    path: "blog/:slug",
    loadComponent: () =>
      import("./features/blog/blog-detail-page.component").then((m) => m.BlogDetailPageComponent),
  },
  {
    path: "cooperation",
    loadComponent: () =>
      import("./features/cooperation/cooperation-page.component").then(
        (m) => m.CooperationPageComponent,
      ),
  },
  {
    path: "rfq",
    canActivate: [authGuard, endUserPortalGuard],
    loadComponent: () =>
      import("./features/rfq/rfq-page.component").then((m) => m.RfqPageComponent),
  },
  { path: "**", redirectTo: "home" },
];
