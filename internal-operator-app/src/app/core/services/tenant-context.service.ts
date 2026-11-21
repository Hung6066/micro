import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { BehaviorSubject, Observable, map, tap } from "rxjs";
import { environment } from "../../../environments/environment";

export interface TenantOption {
  key: string;
  label: string;
  scopeId: string;
  isCustomerSupport: boolean;
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

const STORAGE_KEY = "internalOperator.activeTenantKey";

@Injectable({ providedIn: "root" })
export class TenantContextService {
  private readonly http = inject(HttpClient);
  private readonly optionsSubject = new BehaviorSubject<TenantOption[]>([]);
  private readonly activeTenantKeySubject = new BehaviorSubject<string | null>(
    sessionStorage.getItem(STORAGE_KEY),
  );

  readonly tenantOptions$ = this.optionsSubject.asObservable();
  readonly activeTenantKey$ = this.activeTenantKeySubject.asObservable();

  readonly activeTenantLabel$: Observable<string | null> = this.tenantOptions$.pipe(
    map((options) => {
      const activeKey = this.activeTenantKeySubject.value;
      if (!activeKey) return null;
      return options.find((option) => option.key === activeKey)?.label ?? activeKey;
    }),
  );

  initialize(): Observable<void> {
    return this.http
      .get<SwitchableTenantResponse>(`${environment.adminApiUrl}/me/switchable-tenants`)
      .pipe(
        tap((response) => {
          const options = (response.tenants ?? []).map((tenant) => ({
            key: tenant.key,
            label: tenant.displayName,
            scopeId: tenant.scopeId,
            isCustomerSupport: tenant.isCustomerSupport,
          }));
          this.optionsSubject.next(options);

          const stored = this.activeTenantKeySubject.value;
          const defaultKey =
            stored && options.some((option) => option.key === stored)
              ? stored
              : options.find((option) => option.key === environment.homeTenantKey)?.key ??
                options[0]?.key ??
                null;
          if (defaultKey !== stored) {
            this.setActiveTenant(defaultKey);
          }
        }),
        map(() => void 0),
      );
  }

  setActiveTenant(tenantKey: string | null): void {
    if (tenantKey) {
      sessionStorage.setItem(STORAGE_KEY, tenantKey);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
    this.activeTenantKeySubject.next(tenantKey);
  }

  getActiveTenantKey(): string | null {
    return this.activeTenantKeySubject.value;
  }

  getActiveTenantScopeId(): string | undefined {
    const activeKey = this.activeTenantKeySubject.value;
    if (!activeKey) return undefined;
    return this.optionsSubject.value.find((option) => option.key === activeKey)?.scopeId;
  }
}
