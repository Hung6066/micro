// @ts-nocheck
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';
import { AdminUser, Role, PermissionGroup, Setting, AuditLog, AdminDashboardStats } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
import {
  mockAdminUsers,
  mockRoles,
  mockPermissionGroups,
  mockSettings,
  mockAuditLogs,
  mockDashboardStats,
} from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockAdminService {
  private users = [...mockAdminUsers];
  private roles = [...mockRoles];
  private settings = [...mockSettings];
  private auditLogs = [...mockAuditLogs];
  private dashboardStats = { ...mockDashboardStats };

  private delayMs(): number {
    return 200 + Math.floor(Math.random() * 200);
  }

  private paginate<T>(items: T[], page: number, pageSize: number): PagedResult<T> {
    const start = (page - 1) * pageSize;
    const paged = items.slice(start, start + pageSize);
    return {
      items: paged,
      totalCount: items.length,
      page,
      pageSize,
      hasNextPage: start + pageSize < items.length,
      hasPreviousPage: page > 1,
    };
  }

  // ─── Users ──────────────────────────────────────────────────────────────────

  getUsers(params?: { search?: string; role?: string; page?: number; pageSize?: number }): Observable<PagedResult<AdminUser>> {
    const page = params?.page ?? 1;
    const pageSize = params?.pageSize ?? 10;
    let filtered = [...this.users];
    if (params?.search) {
      const term = params.search.toLowerCase();
      filtered = filtered.filter(
        (u) =>
          u.fullName.toLowerCase().includes(term) ||
          u.email.toLowerCase().includes(term) ||
          u.id.toLowerCase().includes(term),
      );
    }
    if (params?.role) {
      filtered = filtered.filter((u) => u.roles.includes(params.role!));
    }
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getUser(id: string): Observable<AdminUser> {
    const user = this.users.find((u) => u.id === id);
    return of(user!).pipe(delay(this.delayMs()));
  }

  createUser(data: Partial<AdminUser> & { password: string }): Observable<AdminUser> {
    const newUser: AdminUser = {
      id: `usr-${String(this.users.length + 1).padStart(3, '0')}`,
      username: data.username || data.email?.split('@')[0] || '',
      email: data.email || '',
      fullName: data.fullName || '',
      phone: data.phone || '',
      roles: data.roles || [],
      isActive: true,
      createdAt: new Date().toISOString(),
    };
    this.users.unshift(newUser);
    return of(newUser).pipe(delay(this.delayMs()));
  }

  updateUser(id: string, data: Partial<AdminUser>): Observable<AdminUser> {
    const idx = this.users.findIndex((u) => u.id === id);
    if (idx >= 0) {
      this.users[idx] = { ...this.users[idx], ...data, updatedAt: new Date().toISOString() };
    }
    return of(this.users[idx]).pipe(delay(this.delayMs()));
  }

  deactivateUser(id: string): Observable<void> {
    const idx = this.users.findIndex((u) => u.id === id);
    if (idx >= 0) {
      this.users[idx] = { ...this.users[idx], isActive: false, updatedAt: new Date().toISOString() };
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  activateUser(id: string): Observable<void> {
    const idx = this.users.findIndex((u) => u.id === id);
    if (idx >= 0) {
      this.users[idx] = { ...this.users[idx], isActive: true, updatedAt: new Date().toISOString() };
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  assignRoles(userId: string, roleIds: string[]): Observable<AdminUser> {
    const idx = this.users.findIndex((u) => u.id === userId);
    if (idx >= 0) {
      this.users[idx] = {
        ...this.users[idx],
        roles: roleIds,
        updatedAt: new Date().toISOString(),
      };
    }
    return of(this.users[idx]).pipe(delay(this.delayMs()));
  }

  // ─── Roles ──────────────────────────────────────────────────────────────────

  getRoles(): Observable<Role[]> {
    return of([...this.roles]).pipe(delay(this.delayMs()));
  }

  getRole(id: string): Observable<Role> {
    const role = this.roles.find((r) => r.id === id);
    return of(role!).pipe(delay(this.delayMs()));
  }

  createRole(data: { name: string; description: string; permissions: string[] }): Observable<Role> {
    const newRole: Role = {
      id: `role-${data.name.toLowerCase().replace(/\s+/g, '-')}`,
      name: data.name,
      description: data.description,
      permissions: data.permissions,
      isSystem: false,
      usersCount: 0,
      createdAt: new Date().toISOString(),
    };
    this.roles.push(newRole);
    return of(newRole).pipe(delay(this.delayMs()));
  }

  updateRole(id: string, data: { name: string; description: string; permissions: string[] }): Observable<Role> {
    const idx = this.roles.findIndex((r) => r.id === id);
    if (idx >= 0) {
      this.roles[idx] = { ...this.roles[idx], ...data };
    }
    return of(this.roles[idx]).pipe(delay(this.delayMs()));
  }

  deleteRole(id: string): Observable<void> {
    this.roles = this.roles.filter((r) => r.id !== id);
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getPermissions(): Observable<PermissionGroup[]> {
    return of(mockPermissionGroups).pipe(delay(this.delayMs()));
  }

  // ─── Settings ───────────────────────────────────────────────────────────────

  getSettings(): Observable<Setting[]> {
    return of([...this.settings]).pipe(delay(this.delayMs()));
  }

  getSetting(key: string): Observable<Setting> {
    const setting = this.settings.find((s) => s.key === key);
    return of(setting!).pipe(delay(this.delayMs()));
  }

  updateSetting(key: string, value: any): Observable<Setting> {
    const idx = this.settings.findIndex((s) => s.key === key);
    if (idx >= 0) {
      this.settings[idx] = { ...this.settings[idx], value };
    }
    return of(this.settings[idx]).pipe(delay(this.delayMs()));
  }

  bulkUpdateSettings(data: { key: string; value: any }[]): Observable<void> {
    for (const d of data) {
      const idx = this.settings.findIndex((s) => s.key === d.key);
      if (idx >= 0) {
        this.settings[idx] = { ...this.settings[idx], value: d.value };
      }
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  // ─── Audit Logs ─────────────────────────────────────────────────────────────

  getAuditLogs(params?: {
    userId?: string;
    action?: string;
    resourceType?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AuditLog>> {
    const page = params?.page ?? 1;
    const pageSize = params?.pageSize ?? 10;
    let filtered = [...this.auditLogs];
    if (params?.userId) {
      filtered = filtered.filter((l) => l.userId === params.userId);
    }
    if (params?.action) {
      filtered = filtered.filter((l) => l.action === params.action);
    }
    if (params?.resourceType) {
      filtered = filtered.filter((l) => l.resourceType === params.resourceType);
    }
    if (params?.fromDate) {
      filtered = filtered.filter((l) => l.timestamp >= params.fromDate!);
    }
    if (params?.toDate) {
      filtered = filtered.filter((l) => l.timestamp <= params.toDate!);
    }
    return of(this.paginate(filtered, page, pageSize)).pipe(delay(this.delayMs()));
  }

  getAuditLog(id: string): Observable<AuditLog> {
    const log = this.auditLogs.find((l) => l.id === id);
    return of(log!).pipe(delay(this.delayMs()));
  }

  // ─── Dashboard ──────────────────────────────────────────────────────────────

  getDashboardStats(): Observable<AdminDashboardStats> {
    return of({ ...this.dashboardStats }).pipe(delay(this.delayMs()));
  }
}
