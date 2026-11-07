import type { OperatorMobileArea } from "../contracts/mobile.contracts";
import { operatorMobilePermissions } from "./operator-mobile-permissions";

export type OperatorMobileMutationSurface = OperatorMobileArea;

/** Permission required for field mutations in each operator area. */
export function mutationPermission(area: OperatorMobileMutationSurface): string {
  switch (area) {
    case "production":
      return operatorMobilePermissions.production.access;
    case "traceability":
      return operatorMobilePermissions.traceability.access;
    case "quality":
      return operatorMobilePermissions.quality.access;
    case "maintenance":
      return operatorMobilePermissions.maintenance.access;
  }
}
