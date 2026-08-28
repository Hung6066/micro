import { createHisHopePermissionWriteGuard } from "@his-hope/mobile-foundation/angular";

export const mobileWriteGuard = createHisHopePermissionWriteGuard({
  fallbackPath: "/operations/forbidden",
});
