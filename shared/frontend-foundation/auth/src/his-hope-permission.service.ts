import { Injectable, computed, signal } from '@angular/core';

export interface HisHopePermissionSnapshot {
  readonly userId?: string;
  readonly roles?: readonly string[];
  readonly permissions: readonly string[];
  /** OAuth scopes are a UX entitlement hint; APIs remain the authority. */
  readonly scopes?: readonly string[];
  readonly facilityIds?: readonly string[];
  readonly authzVersion?: string;
  readonly issuedAt?: string;
  readonly expiresAt?: string;
}

export interface HisHopeAuthorizationFailure {
  readonly status: 401 | 403;
  readonly action?: string;
  readonly occurredAt: string;
}

@Injectable({ providedIn: 'root' })
export class HisHopePermissionService {
  private readonly snapshotState = signal<HisHopePermissionSnapshot | null>(null);
  private readonly failureState = signal<HisHopeAuthorizationFailure | null>(null);
  readonly snapshot = this.snapshotState.asReadonly();
  readonly lastAuthorizationFailure = this.failureState.asReadonly();
  readonly permissions = computed(() => new Set(this.snapshotState()?.permissions ?? []));
  readonly hasSnapshot = computed(() => this.isUsable(this.snapshotState()));

  setPermissions(permissions: Iterable<string>): void {
    this.setSnapshot({ permissions: Array.from(permissions) });
  }

  setSnapshot(snapshot: HisHopePermissionSnapshot): void {
    this.snapshotState.set({
      ...snapshot,
      roles: this.normalize(snapshot.roles),
      permissions: this.normalize(snapshot.permissions) ?? [],
      scopes: this.normalize(snapshot.scopes),
      facilityIds: this.normalize(snapshot.facilityIds),
    });
  }

  clear(): void {
    this.snapshotState.set(null);
    this.failureState.set(null);
  }

  recordAuthorizationFailure(status: 401 | 403, action?: string): void {
    if (status === 401) this.snapshotState.set(null);
    this.failureState.set({ status, action, occurredAt: new Date().toISOString() });
  }

  clearAuthorizationFailure(): void { this.failureState.set(null); }

  has(permission: string): boolean {
    if (!permission) return true;
    if (!this.hasSnapshot()) return false;
    const available = new Set(this.snapshotState()?.permissions ?? []);
    return available.has('*') || available.has(permission) || Array.from(available).some(value => value.endsWith('.*') && permission.startsWith(value.slice(0, -1)));
  }
  hasScope(scope: string): boolean {
    if (!scope) return true;
    if (!this.hasSnapshot()) return false;
    return (this.snapshotState()?.scopes ?? []).includes(scope);
  }
  hasAnyScope(scopes: Iterable<string>): boolean { return Array.from(scopes).some(scope => this.hasScope(scope)); }
  hasAllScopes(scopes: Iterable<string>): boolean { return Array.from(scopes).every(scope => this.hasScope(scope)); }
  hasAny(permissions: Iterable<string>): boolean { return Array.from(permissions).some(permission => this.has(permission)); }
  hasAll(permissions: Iterable<string>): boolean { return Array.from(permissions).every(permission => this.has(permission)); }

  private normalize(values: readonly string[] | undefined): readonly string[] | undefined {
    if (!values) return undefined;
    return Array.from(new Set(values.map(value => value.trim()).filter(Boolean)));
  }

  private isUsable(snapshot: HisHopePermissionSnapshot | null): boolean {
    if (!snapshot) return false;
    if (!snapshot.expiresAt) return true;
    const expiresAt = Date.parse(snapshot.expiresAt);
    return Number.isFinite(expiresAt) && expiresAt > Date.now();
  }
}
