import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map, retry } from 'rxjs/operators';
import { environment } from '@env/environment';
import { AdminUser, Role, PermissionGroup, Setting, AuditLog, AdminDashboardStats } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly baseUrl = `${environment.apiUrl}/admin`;

  private http = inject(HttpClient);

  // ─── Users ──────────────────────────────────────────────────────────────────

  getUsers(params?: { search?: string; role?: string; page?: number; pageSize?: number }): Observable<PagedResult<AdminUser>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.search) httpParams = httpParams.set('search', params.search);
      if (params.role) httpParams = httpParams.set('role', params.role);
      if (params.page != null) httpParams = httpParams.set('page', params.page);
      if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    }
    return this.http.get<PagedResult<AdminUser>>(`${this.baseUrl}/users`, { params: httpParams }).pipe(
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
    return this.http.get<any[]>(`${this.baseUrl}/settings`).pipe(
      map(settings => settings.map(s => ({
        key: s.key,
        value: this.parseValue(s.value),
        type: this.inferType(s.key, s.value),
        label: s.description || s.key,
        category: s.category || 'general',
      }))),
      retry(1),
      catchError(this.handleError),
    );
  }

  getSetting(key: string): Observable<Setting> {
    return this.http.get<any>(`${this.baseUrl}/settings/${key}`).pipe(
      map(s => ({
        key: s.key,
        value: this.parseValue(s.value),
        type: this.inferType(s.key, s.value),
        label: s.description || s.key,
        category: s.category || 'general',
      })),
      catchError(this.handleError),
    );
  }

  updateSetting(key: string, value: any): Observable<Setting> {
    return this.http.put<any>(`${this.baseUrl}/settings/${key}`, { value: String(value) }).pipe(
      map(s => ({
        key: s.key,
        value: this.parseValue(s.value),
        type: this.inferType(s.key, s.value),
        label: s.description || s.key,
        category: s.category || 'general',
      })),
      catchError(this.handleError),
    );
  }

  bulkUpdateSettings(data: { key: string; value: any }[]): Observable<void> {
    const body = data.map(d => ({ key: d.key, value: String(d.value) }));
    return this.http.put<void>(`${this.baseUrl}/settings/bulk`, { settings: body }).pipe(
      catchError(this.handleError),
    );
  }

  /**
   * Infer the UI type of a setting from its raw string value.
   */
  private inferType(key: string, value: string): Setting['type'] {
    if (value === 'true' || value === 'false') return 'boolean';
    if (/^-?\d+(\.\d+)?$/.test(value)) return 'number';
    return 'text';
  }

  /**
   * Parse a raw string value into the correct JS type for the UI.
   */
  private parseValue(value: string): any {
    if (value === 'true') return true;
    if (value === 'false') return false;
    if (/^-?\d+(\.\d+)?$/.test(value))
      return value.includes('.') ? parseFloat(value) : parseInt(value, 10);
    return value;
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
    let httpParams = new HttpParams();
    if (params) {
      if (params.userId) httpParams = httpParams.set('userId', params.userId);
      if (params.action) httpParams = httpParams.set('action', params.action);
      if (params.resourceType) httpParams = httpParams.set('resourceType', params.resourceType);
      if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
      if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
      if (params.page != null) httpParams = httpParams.set('page', params.page);
      if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    }
    return this.http.get<PagedResult<AuditLog>>(`${this.baseUrl}/audit-logs`, { params: httpParams }).pipe(
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
