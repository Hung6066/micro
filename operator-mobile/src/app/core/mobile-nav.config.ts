import { HisHopeMobileNavItem } from "@his-hope/frontend-foundation/ui";
import {
  dashboardReadPermission,
  readPermission,
} from "./authorization/mobile-read-permissions";

/** Bottom navigation entries, ordered as they appear in the shell. */
export const MOBILE_NAV_ITEMS: readonly HisHopeMobileNavItem[] = [
  {
    route: "/operations",
    icon: "home",
    labelKey: "admin.dashboard",
    labelFallback: "Dashboard",
    permission: dashboardReadPermission(),
  },
  {
    route: "/admin/clients",
    icon: "clients",
    labelKey: "admin.clients",
    labelFallback: "Clients",
    permission: readPermission("clients"),
  },
  {
    route: "/admin/users",
    icon: "users",
    labelKey: "admin.users",
    labelFallback: "Users",
    permission: readPermission("users"),
  },
  {
    route: "/admin/roles",
    icon: "roles",
    labelKey: "admin.roles",
    labelFallback: "Roles",
    permission: readPermission("roles"),
  },
  {
    route: "/admin/consents",
    icon: "consents",
    labelKey: "admin.consents",
    labelFallback: "Consents",
    permission: readPermission("consents"),
  },
];
