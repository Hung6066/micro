import { MobileResource } from "../contracts/mobile.contracts";

export type MobileMutationSurface = MobileResource | "mfa" | "notifications";

/** Single source of truth for the permission each mobile mutation requires. */
export function mutationPermission(
  surface: MobileMutationSurface,
): string {
  switch (surface) {
    case "clients":
      return "admin.clients.write";
    case "users":
      return "admin.users.write";
    case "roles":
      return "admin.roles.write";
    case "consents":
      return "admin.consents.write";
    case "mfa":
      return "admin.credentials.reset";
    case "notifications":
      return "admin.users.write";
  }
}
