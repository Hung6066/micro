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
    path: "inventory/lots",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/inventory/lots-page.component").then(
        (m) => m.LotsPageComponent,
      ),
  },
  {
    path: "production",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/production/production-page.component").then(
        (m) => m.ProductionPageComponent,
      ),
  },
  {
    path: "procurement",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/procurement/procurement-page.component").then(
        (m) => m.ProcurementPageComponent,
      ),
  },
  {
    path: "recipes",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/recipes/recipes-page.component").then(
        (m) => m.RecipesPageComponent,
      ),
  },
  {
    path: "product-specifications",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/product-specifications/product-specifications-page.component").then(
        (m) => m.ProductSpecificationsPageComponent,
      ),
  },
  {
    path: "quality-inspections",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/quality-inspections/quality-inspections-page.component").then(
        (m) => m.QualityInspectionsPageComponent,
      ),
  },
  {
    path: "deviations",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/deviations/deviations-page.component").then(
        (m) => m.DeviationsPageComponent,
      ),
  },
  {
    path: "capas",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () => import("./features/capas/capas-page.component").then((m) => m.CapasPageComponent),
  },
  {
    path: "forecast",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/forecast/forecast-page.component").then(
        (m) => m.ForecastPageComponent,
      ),
  },
  {
    path: "sales-allocation",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/sales-allocation/sales-allocation-page.component").then(
        (m) => m.SalesAllocationPageComponent,
      ),
  },
  {
    path: "maintenance",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/maintenance/maintenance-page.component").then(
        (m) => m.MaintenancePageComponent,
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
  {
    path: "content",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/content/content-page.component").then((m) => m.ContentPageComponent),
  },
  {
    path: "rfqs",
    canActivate: [authGuard, operatorPortalGuard],
    loadComponent: () =>
      import("./features/rfqs/rfqs-page.component").then((m) => m.RfqsPageComponent),
  },
  { path: "**", redirectTo: "dashboard" },
];
