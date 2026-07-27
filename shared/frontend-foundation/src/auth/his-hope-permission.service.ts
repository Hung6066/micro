import { Injectable, computed, signal } from '@angular/core';

export interface HisHopePermissionSnapshot {
  readonly userId?: string;
  readonly roles?: readonly string[];
  readonly permissions: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class HisHopePermissionService {
  private readonly permissionState = signal<ReadonlySet<string>>(new Set());
  readonly permissions = this.permissionState.asReadonly();
  readonly hasSnapshot = computed(() => this.permissionState().size > 0);

  setPermissions(permissions: Iterable<string>): void {
    this.permissionState.set(new Set(Array.from(permissions).map(permission => permission.trim()).filter(Boolean)));
  }

  setSnapshot(snapshot: HisHopePermissionSnapshot): void {
    this.setPermissions(snapshot.permissions);
  }

  clear(): void { this.permissionState.set(new Set()); }

  has(permission: string): boolean {
    if (!permission) return true;
    const available = this.permissionState();
    return available.has('*') || available.has(permission) || Array.from(available).some(value => value.endsWith('.*') && permission.startsWith(value.slice(0, -1)));
  }
  hasAny(permissions: Iterable<string>): boolean { return Array.from(permissions).some(permission => this.has(permission)); }
  hasAll(permissions: Iterable<string>): boolean { return Array.from(permissions).every(permission => this.has(permission)); }
}
