import { Routes } from "@angular/router";
import { mobileAuthGuard } from "./core/auth.guard";
import { OperatorMobileAppComponent } from "./operator-mobile-app.component";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "operations" },
  { path: "auth/login", loadComponent: () => import("./mobile-login.component").then((m) => m.MobileLoginComponent) },
  { path: "auth/callback", loadComponent: () => import("./mobile-callback.component").then((m) => m.MobileCallbackComponent) },
  {
    path: "operations",
    component: OperatorMobileAppComponent,
    canActivate: [mobileAuthGuard],
    children: [
      { path: "", pathMatch: "full", redirectTo: "production" },
      { path: "production", loadComponent: () => import("./features/production/production-work-page.component").then((m) => m.ProductionWorkPageComponent) },
      { path: "traceability", loadComponent: () => import("./features/traceability/lot-scan-page.component").then((m) => m.LotScanPageComponent) },
      { path: "quality", loadComponent: () => import("./features/quality/quality-inspection-page.component").then((m) => m.QualityInspectionPageComponent) },
      { path: "maintenance", loadComponent: () => import("./features/maintenance/maintenance-work-page.component").then((m) => m.MaintenanceWorkPageComponent) },
      { path: "sync", loadComponent: () => import("./features/sync/sync-page.component").then((m) => m.SyncPageComponent) },
    ],
  },
  { path: "**", redirectTo: "operations" },
];
