import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { environment } from '@env/environment';
import { AdminUser, Role, PermissionGroup, Setting, AuditLog, AdminDashboardStats } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly baseUrl = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  // ─── Users ──────────────────────────────────────────────────────────────────

  getUsers(params?: { search?: string; role?: string; page?: number; pageSize?: number }): Observable<PagedResult<AdminUser>> {
    return this.http.get<PagedResult<AdminUser>>(`${this.baseUrl}/users`, { params: params as any }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getUser(id: string): Observable<AdminUser> {
    return this.http.get<AdminUser>(`${this.baseUrl}/users/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createUser(data: Partial<AdminUser> & { password: string }): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.baseUrl}/users`, data).pipe(
      catchError(this.handleError),
    );
  }

  updateUser(id: string, data: Partial<AdminUser>): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.baseUrl}/users/${id}`, data).pipe(
      catchError(this.handleError),
    );
  }

  deactivateUser(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/users/${id}/deactivate`, {}).pipe(
      catchError(this.handleError),
    );
  }

  activateUser(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/users/${id}/activate`, {}).pipe(
      catchError(this.handleError),
    );
  }

  assignRoles(userId: string, roleIds: string[]): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.baseUrl}/users/${userId}/roles`, { roleIds }).pipe(
      catchError(this.handleError),
    );
  }

  // ─── Roles ──────────────────────────────────────────────────────────────────

  getRoles(): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.baseUrl}/roles`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getRole(id: string): Observable<Role> {
    return this.http.get<Role>(`${this.baseUrl}/roles/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createRole(data: { name: string; description: string; permissions: string[] }): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/roles`, data).pipe(
      catchError(this.handleError),
    );
  }

  updateRole(id: string, data: { name: string; description: string; permissions: string[] }): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/roles/${id}`, data).pipe(
      catchError(this.handleError),
    );
  }

  deleteRole(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/roles/${id}`).pipe(
      catchError(this.handleError),
    );
  }

  getPermissions(): Observable<PermissionGroup[]> {
    return this.http.get<PermissionGroup[]>(`${this.baseUrl}/permissions`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  // ─── Settings ───────────────────────────────────────────────────────────────

  getSettings(): Observable<Setting[]> {
    return this.http.get<Setting[]>(`${this.baseUrl}/settings`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getSetting(key: string): Observable<Setting> {
    return this.http.get<Setting>(`${this.baseUrl}/settings/${key}`).pipe(
      catchError(this.handleError),
    );
  }

  updateSetting(key: string, value: any): Observable<Setting> {
    return this.http.put<Setting>(`${this.baseUrl}/settings/${key}`, { value }).pipe(
      catchError(this.handleError),
    );
  }

  bulkUpdateSettings(data: { key: string; value: any }[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/settings/bulk`, { settings: data }).pipe(
      catchError(this.handleError),
    );
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
    return this.http.get<PagedResult<AuditLog>>(`${this.baseUrl}/audit-logs`, { params: params as any }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getAuditLog(id: string): Observable<AuditLog> {
    return this.http.get<AuditLog>(`${this.baseUrl}/audit-logs/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  // ─── Dashboard ──────────────────────────────────────────────────────────────

  getDashboardStats(): Observable<AdminDashboardStats> {
    return this.http.get<AdminDashboardStats>(`${this.baseUrl}/dashboard`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  // ─── Error Handler ──────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Lỗi không xác định';
    if (error.error instanceof ErrorEvent) {
      errorMessage = `Lỗi máy khách: ${error.error.message}`;
    } else {
      errorMessage = `Lỗi máy chủ: ${error.status} - ${error.message}`;
    }
    console.error('[AdminService]', errorMessage);
    return throwError(() => error);
  }
}
