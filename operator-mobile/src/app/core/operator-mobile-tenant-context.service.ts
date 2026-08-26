import { Injectable, inject, signal } from "@angular/core";
import { MobileAuthService } from "./auth.service";
import { OperationQueueService } from "./offline/operation-queue.service";

@Injectable({ providedIn: "root" })
export class OperatorMobileTenantContextService {
  private readonly queue = inject(OperationQueueService);
  private readonly auth = inject(MobileAuthService);
  readonly activeTenantKey = signal<string | null>(null);
  readonly subjectId = signal<string | null>(null);
  readonly availableTenants = signal<string[]>([]);

  constructor() {
    this.auth.userData$.subscribe((result) => {
      const data = (result as { userData?: Record<string, unknown> } | null)?.userData;
      const subject = typeof data?.["sub"] === "string" ? data["sub"] : null;
      const tenant = typeof data?.["tenant_id"] === "string"
        ? data["tenant_id"]
        : typeof data?.["tenant"] === "string" ? data["tenant"] : null;
      const tenantClaims = data?.["tenant_ids"] ?? data?.["tenants"];
      const tenants = Array.isArray(tenantClaims)
        ? tenantClaims.map((value) => typeof value === "string" ? value : typeof value === "object" && value !== null && typeof value["tenant_id"] === "string" ? value["tenant_id"] : null).filter((value): value is string => Boolean(value))
        : [];
      this.subjectId.set(subject);
      const options = [...new Set([...(tenant ? [tenant] : []), ...tenants])];
      this.availableTenants.set(options);
      if (options.length && !this.activeTenantKey()) this.activeTenantKey.set(options[0]);
    });
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
