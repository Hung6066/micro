import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { firstValueFrom } from "rxjs";
import { MobileAuthService } from "./auth.service";
import { OperationQueueService } from "./offline/operation-queue.service";
import { environment } from "../../environments/environment";

const STORAGE_KEY = "operatorMobile.activeTenantKey";

export interface OperatorMobileTenantOption {
  key: string;
  label: string;
}

interface SwitchableTenantResponse {
  tenants: Array<{
    key: string;
    displayName: string;
    scopeId: string;
    tenantClass: string;
    isCustomerSupport: boolean;
  }>;
}

@Injectable({ providedIn: "root" })
export class OperatorMobileTenantContextService {
  private readonly queue = inject(OperationQueueService);
  private readonly auth = inject(MobileAuthService);
  private readonly http = inject(HttpClient);
  readonly activeTenantKey = signal<string | null>(readStoredTenantKey());
  readonly subjectId = signal<string | null>(null);
  readonly email = signal<string | null>(null);
  readonly tenantOptions = signal<OperatorMobileTenantOption[]>([]);

  /** Backwards-compatible alias for templates that iterate tenant keys. */
  readonly availableTenants = this.tenantOptions;

  constructor() {
    this.auth.userData$.subscribe((result) => {
      const data = (result as { userData?: Record<string, unknown> } | null)?.userData;
      const subject = typeof data?.["sub"] === "string" ? data["sub"] : null;
      const email = [data?.["email"], data?.["preferred_username"], data?.["upn"]]
        .find((value): value is string => typeof value === "string" && value.includes("@")) ?? null;
      this.subjectId.set(subject);
      this.email.set(email);

      if (!this.tenantOptions().length) {
        this.applyJwtTenantOptions(data);
      }

      this.auth.getCurrentUserProfile().subscribe({
        next: (profile) => {
          if (profile.email) this.email.set(profile.email);
          if (!this.subjectId()) this.subjectId.set(profile.id);
        },
      });
    });
  }

  getActiveTenantKey(): string | null {
    return this.activeTenantKey();
  }

  async initialize(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<SwitchableTenantResponse>(
          `${environment.adminApiUrl}/me/switchable-tenants`,
        ),
      );
      const options = (response.tenants ?? []).map((tenant) => ({
        key: tenant.key,
        label: tenant.displayName || tenant.key,
      }));
      if (options.length) {
        this.tenantOptions.set(options);
        await this.ensureActiveTenant(options);
        return;
      }
    } catch {
      // Admin tenant discovery is best-effort; JWT claims remain the fallback.
    }

    if (!this.tenantOptions().length) {
      this.applyJwtTenantOptions(undefined);
    }
    await this.ensureActiveTenant(this.tenantOptions());
  }

  async setActiveTenant(tenantKey: string): Promise<void> {
    const normalized = tenantKey.trim();
    if (!normalized || normalized === this.activeTenantKey()) return;
    this.persistActiveTenant(normalized);
    if (this.subjectId()) {
      await this.queue.retainScope(normalized, this.subjectId()!);
    }
  }

  get commandScope(): { tenantKey: string; subjectId: string } | null {
    const tenantKey = this.activeTenantKey();
    const subjectId = this.subjectId();
    return tenantKey && subjectId ? { tenantKey, subjectId } : null;
  }

  private async ensureActiveTenant(options: OperatorMobileTenantOption[]): Promise<void> {
    if (!options.length) {
      this.persistActiveTenant(null);
      return;
    }

    const stored = this.activeTenantKey();
    const defaultKey =
      stored && options.some((option) => option.key === stored)
        ? stored
        : options.find((option) => option.key === environment.homeTenantKey)?.key ??
          options[0]?.key ??
          null;

    if (defaultKey) {
      await this.setActiveTenant(defaultKey);
    }
  }

  private applyJwtTenantOptions(data: Record<string, unknown> | undefined): void {
    const keys = [...new Set(this.readTenantClaims(data).length ? this.readTenantClaims(data) : [environment.homeTenantKey])]
      .filter(Boolean);
    this.tenantOptions.set(keys.map((key) => ({ key, label: key })));
    if (keys.length && !this.activeTenantKey()) {
      this.persistActiveTenant(keys[0]!);
    }
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

  private persistActiveTenant(tenantKey: string | null): void {
    if (tenantKey) {
      sessionStorage.setItem(STORAGE_KEY, tenantKey);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
    this.activeTenantKey.set(tenantKey);
  }
}

function readStoredTenantKey(): string | null {
  if (typeof sessionStorage === "undefined") return null;
  return sessionStorage.getItem(STORAGE_KEY);
}
