import { MobileResource } from "../contracts/mobile.contracts";

/**
 * Mirrors the permission Identity Service enforces on each admin read
 * endpoint. `/consents` and `/dashboard` are both guarded by
 * `admin.users.read` server-side, so the client must not invent narrower keys
 * that would hide screens the operator is actually allowed to open.
 */
export function readPermission(resource: MobileResource): string {
  switch (resource) {
    case "clients":
      return "admin.clients.read";
    case "users":
      return "admin.users.read";
    case "roles":
      return "admin.roles.read";
    case "consents":
      return "admin.users.read";
  }
}

export function dashboardReadPermission(): string {
  return "admin.users.read";
}
