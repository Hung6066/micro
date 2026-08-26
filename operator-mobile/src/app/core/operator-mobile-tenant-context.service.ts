import { Injectable, inject, signal } from "@angular/core";
import { MobileAuthService } from "./auth.service";
import { OperationQueueService } from "./offline/operation-queue.service";

@Injectable({ providedIn: "root" })
export class OperatorMobileTenantContextService {
  private readonly queue = inject(OperationQueueService);
  private readonly auth = inject(MobileAuthService);
  readonly activeTenantKey = signal<string | null>(null);
  readonly subjectId = signal<string | null>(null);
  readonly email = signal<string | null>(null);
  readonly availableTenants = signal<string[]>([]);

  constructor() {
    this.auth.userData$.subscribe((result) => {
      const data = (result as { userData?: Record<string, unknown> } | null)?.userData;
      const subject = typeof data?.["sub"] === "string" ? data["sub"] : null;
      const email = [data?.["email"], data?.["preferred_username"], data?.["upn"]]
        .find((value): value is string => typeof value === "string" && value.includes("@")) ?? null;
      const tenants = this.readTenantClaims(data);
      this.subjectId.set(subject);
      this.email.set(email);
      // The operator client is permanently bound to manufacturing. Keep the
      // UI usable with older tokens that predate tenant claims; API
      // authorization remains authoritative on every request.
      const options = [...new Set(tenants.length ? tenants : ["manufacturing"])]
        .filter((value): value is string => Boolean(value));
      this.availableTenants.set(options);
      if (options.length && !this.activeTenantKey()) this.activeTenantKey.set(options[0]);
      this.auth.getCurrentUserProfile().subscribe({
        next: (profile) => {
          if (profile.email) this.email.set(profile.email);
          if (!this.subjectId()) this.subjectId.set(profile.id);
        },
      });
    });
  }

  private readTenantClaims(data: Record<string, unknown> | undefined): string[] {
    if (!data) return [];
    const values = [
      data["tenant_id"],
      data["tenant"],
      data["tenant_membership"],
      data["tenant_memberships"],
      data["tenant_ids"],
      data["tenants"],
    ];
    return values
      .flatMap((value) => {
        if (typeof value === "string") return value.split(",");
        if (Array.isArray(value)) return value;
        if (value && typeof value === "object" && "tenant_id" in value) return [value.tenant_id];
        return [];
      })
      .filter((value): value is string => typeof value === "string")
      .map((value) => value.trim())
      .filter(Boolean);
  }

  async setActiveTenant(tenantKey: string): Promise<void> {
    const normalized = tenantKey.trim();
    if (!normalized || normalized === this.activeTenantKey() || !this.subjectId()) return;
    this.activeTenantKey.set(normalized);
    await this.queue.retainScope(normalized, this.subjectId()!);
  }

  get commandScope(): { tenantKey: string; subjectId: string } | null {
    const tenantKey = this.activeTenantKey();
    const subjectId = this.subjectId();
    return tenantKey && subjectId ? { tenantKey, subjectId } : null;
  }
}
