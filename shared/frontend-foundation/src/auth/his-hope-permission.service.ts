import { Injectable, computed, signal } from '@angular/core';

export interface HisHopePermissionSnapshot {
  readonly userId?: string;
  readonly roles?: readonly string[];
  readonly permissions: readonly string[];
  readonly facilityIds?: readonly string[];
  readonly authzVersion?: string;
  readonly issuedAt?: string;
  readonly expiresAt?: string;
}

@Injectable({ providedIn: 'root' })
export class HisHopePermissionService {
  private readonly snapshotState = signal<HisHopePermissionSnapshot | null>(null);
  readonly snapshot = this.snapshotState.asReadonly();
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
      facilityIds: this.normalize(snapshot.facilityIds),
    });
  }

  clear(): void { this.snapshotState.set(null); }

  has(permission: string): boolean {
    if (!permission) return true;
    if (!this.hasSnapshot()) return false;
    const available = new Set(this.snapshotState()?.permissions ?? []);
    return available.has('*') || available.has(permission) || Array.from(available).some(value => value.endsWith('.*') && permission.startsWith(value.slice(0, -1)));
  }
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
