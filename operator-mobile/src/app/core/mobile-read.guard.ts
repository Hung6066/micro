import { createHisHopePermissionReadGuard } from "@his-hope/mobile-foundation/angular";
import { readPermission } from "./authorization/mobile-read-permissions";
import type { OperatorMobileArea } from "./contracts/mobile.contracts";

export const mobileReadGuard = createHisHopePermissionReadGuard({
  forbiddenPath: "/operations/forbidden",
  resolvePermission: (route) => {
    const area = route.data["area"] as OperatorMobileArea | undefined;
    return (
      (route.data["readPermission"] as string | undefined) ??
      (area ? readPermission(area) : undefined)
    );
  },
  resolveForbiddenQuery: (route, permission) => ({
    resource:
      (route.data["area"] as string | undefined) ??
      (route.data["resource"] as string | undefined) ??
      permission,
  }),
});
