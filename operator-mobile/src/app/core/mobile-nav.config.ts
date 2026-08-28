import type { OperatorMobileArea } from "./contracts/mobile.contracts";
import { readPermission } from "./authorization/mobile-read-permissions";

export interface OperatorMobileNavItem {
  readonly route: OperatorMobileArea | "sync";
  readonly icon: string;
  readonly labelKey: string;
  readonly labelFallback: string;
  readonly permission?: string;
}

/** Bottom navigation entries for the operator shell. */
export const OPERATOR_MOBILE_NAV_ITEMS: readonly OperatorMobileNavItem[] = [
  {
    route: "production",
    icon: "precision_manufacturing",
    labelKey: "mobile.operatorProduction",
    labelFallback: "Production",
    permission: readPermission("production"),
  },
  {
    route: "traceability",
    icon: "qr_code_scanner",
    labelKey: "mobile.operatorTraceability",
    labelFallback: "Traceability",
    permission: readPermission("traceability"),
  },
  {
    route: "quality",
    icon: "fact_check",
    labelKey: "mobile.operatorQuality",
    labelFallback: "Quality",
    permission: readPermission("quality"),
  },
  {
    route: "maintenance",
    icon: "build",
    labelKey: "mobile.operatorMaintenance",
    labelFallback: "Maintenance",
    permission: readPermission("maintenance"),
  },
  {
    route: "sync",
    icon: "sync",
    labelKey: "mobile.operatorSync",
    labelFallback: "Sync",
  },
];
