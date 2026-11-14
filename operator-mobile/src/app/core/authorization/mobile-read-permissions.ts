import type { OperatorMobileArea } from "../contracts/mobile.contracts";
import { operatorMobilePermissions } from "./operator-mobile-permissions";

/** Read/route permission for each operator mobile area. */
export function readPermission(area: OperatorMobileArea): string {
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
