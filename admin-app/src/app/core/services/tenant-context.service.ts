import { DestroyRef, inject, Injectable } from "@angular/core";
import {
  BehaviorSubject,
  Observable,
  combineLatest,
  forkJoin,
  map,
  of,
  switchMap,
  tap,
} from "rxjs";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamScope } from "../contracts/admin.contracts";
import { AdminPermissionsApiService } from "./admin-permissions-api.service";
import { IamApiService } from "./iam-api.service";

export interface TenantOption {
  key: string;
  label: string;
  scopeId: string;
}

const STORAGE_KEY = "admin.activeTenantKey";

@Injectable({ providedIn: "root" })
export class TenantContextService {
  private readonly permissionsApi = inject(AdminPermissionsApiService);
  private readonly iamApi = inject(IamApiService);

  private readonly scopesSubject = new BehaviorSubject<IamScope[]>([]);
  private readonly membershipsSubject = new BehaviorSubject<string[]>([]);
  private readonly activeTenantKeySubject = new BehaviorSubject<string | null>(
    sessionStorage.getItem(STORAGE_KEY),
  );

  readonly scopes$ = this.scopesSubject.asObservable();
  readonly activeTenantKey$ = this.activeTenantKeySubject.asObservable();
  readonly tenantOptions$: Observable<TenantOption[]> = combineLatest([
    this.scopes$,
    this.membershipsSubject,
  ]).pipe(
    map(([scopes, memberships]) => this.buildTenantOptions(scopes, memberships)),
  );

  readonly activeTenantLabel$: Observable<string | null> = combineLatest([
    this.tenantOptions$,
    this.activeTenantKey$,
  ]).pipe(
    map(([options, activeKey]) => {
      if (!activeKey) return null;
      return options.find((option) => option.key === activeKey)?.label ?? activeKey;
    }),
  );

  initialize(): Observable<void> {
    return forkJoin({
      permissions: this.permissionsApi.getCurrent(),
      scopes: this.iamApi.getIamScopes(),
    }).pipe(
      tap(({ permissions, scopes }) => {
        this.scopesSubject.next(scopes);
        const memberships =
          permissions.tenantMemberships?.filter(Boolean) ??
          (permissions.tenantId ? [permissions.tenantId] : []);
        this.membershipsSubject.next(memberships);

        const stored = this.activeTenantKeySubject.value;
        const preferredMembership = memberships.includes("group-hq")
          ? "group-hq"
          : memberships[0];
        const defaultKey =
          stored && memberships.includes(stored)
            ? stored
            : permissions.tenantId && memberships.includes(permissions.tenantId)
              ? permissions.tenantId
              : preferredMembership ?? null;
        if (defaultKey !== stored) {
          this.setActiveTenant(defaultKey);
        }
      }),
      switchMap(() => of(void 0)),
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
    return this.findTenantScope(this.activeTenantKeySubject.value)?.id;
  }

  isGroupHqOperator(): boolean {
    return this.membershipsSubject.value.includes("group-hq");
  }

  getActiveEnvironmentScopeId(): string | undefined {
    const tenantScope = this.findTenantScope(this.activeTenantKeySubject.value);
    if (!tenantScope) return undefined;
    const scopes = this.scopesSubject.value;
    const account = scopes.find(
      (scope) => scope.kind === "account" && scope.parentId === tenantScope.id,
    );
    if (!account) return undefined;
    return scopes.find(
      (scope) => scope.kind === "environment" && scope.parentId === account.id,
    )?.id;
  }

  filterScopes(scopes: IamScope[]): IamScope[] {
    const tenantKey = this.activeTenantKeySubject.value;
    if (!tenantKey) return scopes;

    const tenantScope = this.findTenantScope(tenantKey);
    if (!tenantScope) return scopes;

    const allowedIds = new Set<string>();
    const queue = [tenantScope.id];
    while (queue.length > 0) {
      const current = queue.shift()!;
      allowedIds.add(current);
      for (const scope of scopes) {
        if (scope.parentId === current) {
          queue.push(scope.id);
        }
      }
    }

    const organization = scopes.find(
      (scope) => scope.kind === "organization" && scope.id === tenantScope.parentId,
    );
    if (organization) {
      allowedIds.add(organization.id);
    }

    return scopes.filter((scope) => allowedIds.has(scope.id));
  }

  filterByScopeId<T extends { scopeId?: string }>(
    rows: T[],
    scopeId: string | undefined,
  ): T[] {
    if (!scopeId) return rows;
    return rows.filter((row) => row.scopeId === scopeId);
  }

  whenTenantChanges<T>(loader: (scopeIds: {
    tenantScopeId?: string;
    environmentScopeId?: string;
  }) => Observable<T>): Observable<T> {
    return this.activeTenantKey$.pipe(
      switchMap(() =>
        loader({
          tenantScopeId: this.getActiveTenantScopeId(),
          environmentScopeId: this.getActiveEnvironmentScopeId(),
        }),
      ),
    );
  }

  /** Subscribe to tenant changes and reload page data (replaces repeated activeTenantKey$ boilerplate). */
  bindTenantReload(destroyRef: DestroyRef, reload: () => void): void {
    this.activeTenantKey$
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => reload());
  }

  /** Returns scope ids for loaders that need explicit environment override. */
  readonly scopeIds = {
    tenant: () => this.getActiveTenantScopeId(),
    environment: () => this.getActiveEnvironmentScopeId(),
  };

  private buildTenantOptions(
    scopes: IamScope[],
    memberships: string[],
  ): TenantOption[] {
    const tenantScopes = scopes.filter((scope) => scope.kind === "tenant");
    const isGroupHqOperator = memberships.includes("group-hq");
    const visible = isGroupHqOperator
      ? tenantScopes
      : memberships.length
        ? tenantScopes.filter((scope) => memberships.includes(scope.key))
        : tenantScopes;
    return visible.map((scope) => ({
      key: scope.key,
      label: scope.displayName,
      scopeId: scope.id,
    }));
  }

  private findTenantScope(tenantKey: string | null): IamScope | undefined {
    if (!tenantKey) return undefined;
    return this.scopesSubject.value.find(
      (scope) => scope.kind === "tenant" && scope.key === tenantKey,
    );
  }
}
