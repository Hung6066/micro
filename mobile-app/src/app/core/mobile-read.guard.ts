import { createHisHopePermissionReadGuard } from "@his-hope/mobile-foundation/angular";
import { readPermission } from "./authorization/mobile-read-permissions";
import { MobileResource } from "./contracts/mobile.contracts";

export const mobileReadGuard = createHisHopePermissionReadGuard({
  forbiddenPath: "/admin/forbidden",
  resolvePermission: (route) => {
    const resource = route.data["resource"] as MobileResource | undefined;
    return (
      (route.data["readPermission"] as string | undefined) ??
      (resource ? readPermission(resource) : undefined)
    );
  },
});
