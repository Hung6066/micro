import { Injectable, computed, inject } from "@angular/core";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { mutationPermission } from "../authorization/mobile-mutation-permissions";
import { readPermission } from "../authorization/mobile-read-permissions";
import type { OperatorMobileArea } from "../contracts/mobile.contracts";

export type OperatorMobileFeatureKey = OperatorMobileArea | "sync";

/** Permission-derived feature flags for operator mobile screens and nav. */
@Injectable({ providedIn: "root" })
export class MobileCapabilitiesService {
  private readonly permissions = inject(HisHopePermissionService);

  readonly canViewProduction = computed(() =>
    this.permissions.has(readPermission("production")),
  );
  readonly canViewTraceability = computed(() =>
    this.permissions.has(readPermission("traceability")),
  );
  readonly canViewQuality = computed(() =>
    this.permissions.has(readPermission("quality")),
  );
  readonly canViewMaintenance = computed(() =>
    this.permissions.has(readPermission("maintenance")),
  );
  readonly canMutateProduction = computed(() =>
    this.permissions.has(mutationPermission("production")),
  );
  readonly canMutateTraceability = computed(() =>
    this.permissions.has(mutationPermission("traceability")),
  );
  readonly canMutateQuality = computed(() =>
    this.permissions.has(mutationPermission("quality")),
  );
  readonly canMutateMaintenance = computed(() =>
    this.permissions.has(mutationPermission("maintenance")),
  );

  isFeatureEnabled(key: OperatorMobileFeatureKey): boolean {
    switch (key) {
      case "sync":
        return true;
      case "production":
        return this.canViewProduction();
      case "traceability":
        return this.canViewTraceability();
      case "quality":
        return this.canViewQuality();
      case "maintenance":
        return this.canViewMaintenance();
    }
  }
}
