import { Injectable, computed, inject } from "@angular/core";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { mutationPermission } from "../authorization/mobile-mutation-permissions";
import {
  dashboardReadPermission,
  readPermission,
} from "../authorization/mobile-read-permissions";

export type MobileFeatureKey =
  | "dashboard"
  | "clients"
  | "users"
  | "roles"
  | "consents"
  | "manageClients"
  | "manageUsers"
  | "manageRoles";

/** Permission-derived feature flags for mobile screens and nav entries. */
@Injectable({ providedIn: "root" })
export class MobileCapabilitiesService {
  private readonly permissions = inject(HisHopePermissionService);

  readonly canViewDashboard = computed(() =>
    this.permissions.has(dashboardReadPermission()),
  );
  readonly canViewClients = computed(() =>
    this.permissions.has(readPermission("clients")),
  );
  readonly canViewUsers = computed(() =>
    this.permissions.has(readPermission("users")),
  );
  readonly canViewRoles = computed(() =>
    this.permissions.has(readPermission("roles")),
  );
  readonly canViewConsents = computed(() =>
    this.permissions.has(readPermission("consents")),
  );
  readonly canManageClients = computed(() =>
    this.permissions.has(mutationPermission("clients", "write")),
  );
  readonly canManageUsers = computed(() =>
    this.permissions.has(mutationPermission("users", "write")),
  );
  readonly canManageRoles = computed(() =>
    this.permissions.has(mutationPermission("roles", "write")),
  );

  isFeatureEnabled(key: MobileFeatureKey): boolean {
    switch (key) {
      case "dashboard":
        return this.canViewDashboard();
      case "clients":
        return this.canViewClients();
      case "users":
        return this.canViewUsers();
      case "roles":
        return this.canViewRoles();
      case "consents":
        return this.canViewConsents();
      case "manageClients":
        return this.canManageClients();
      case "manageUsers":
        return this.canManageUsers();
      case "manageRoles":
        return this.canManageRoles();
    }
  }
}
