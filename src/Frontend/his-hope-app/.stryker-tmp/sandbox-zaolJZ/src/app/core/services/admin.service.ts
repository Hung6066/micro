// @ts-nocheck
function stryNS_9fa48() {
  var g = typeof globalThis === 'object' && globalThis && globalThis.Math === Math && globalThis || new Function("return this")();
  var ns = g.__stryker__ || (g.__stryker__ = {});
  if (ns.activeMutant === undefined && g.process && g.process.env && g.process.env.__STRYKER_ACTIVE_MUTANT__) {
    ns.activeMutant = g.process.env.__STRYKER_ACTIVE_MUTANT__;
  }
  function retrieveNS() {
    return ns;
  }
  stryNS_9fa48 = retrieveNS;
  return retrieveNS();
}
stryNS_9fa48();
function stryCov_9fa48() {
  var ns = stryNS_9fa48();
  var cov = ns.mutantCoverage || (ns.mutantCoverage = {
    static: {},
    perTest: {}
  });
  function cover() {
    var c = cov.static;
    if (ns.currentTestId) {
      c = cov.perTest[ns.currentTestId] = cov.perTest[ns.currentTestId] || {};
    }
    var a = arguments;
    for (var i = 0; i < a.length; i++) {
      c[a[i]] = (c[a[i]] || 0) + 1;
    }
  }
  stryCov_9fa48 = cover;
  cover.apply(null, arguments);
}
function stryMutAct_9fa48(id) {
  var ns = stryNS_9fa48();
  function isActive(id) {
    if (ns.activeMutant === id) {
      if (ns.hitCount !== void 0 && ++ns.hitCount > ns.hitLimit) {
        throw new Error('Stryker: Hit count limit reached (' + ns.hitCount + ')');
      }
      return true;
    }
    return false;
  }
  stryMutAct_9fa48 = isActive;
  return isActive(id);
}
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { environment } from '@env/environment';
import { AdminUser, Role, PermissionGroup, Setting, AuditLog, AdminDashboardStats } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly baseUrl = stryMutAct_9fa48("225") ? `` : (stryCov_9fa48("225"), `${environment.apiUrl}/admin`);
  constructor(private http: HttpClient) {}

  // ─── Users ──────────────────────────────────────────────────────────────────

  getUsers(params?: {
    search?: string;
    role?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AdminUser>> {
    if (stryMutAct_9fa48("226")) {
      {}
    } else {
      stryCov_9fa48("226");
      return this.http.get<PagedResult<AdminUser>>(stryMutAct_9fa48("227") ? `` : (stryCov_9fa48("227"), `${this.baseUrl}/users`), stryMutAct_9fa48("228") ? {} : (stryCov_9fa48("228"), {
        params: params as any
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getUser(id: string): Observable<AdminUser> {
    if (stryMutAct_9fa48("229")) {
      {}
    } else {
      stryCov_9fa48("229");
      return this.http.get<AdminUser>(stryMutAct_9fa48("230") ? `` : (stryCov_9fa48("230"), `${this.baseUrl}/users/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createUser(data: Partial<AdminUser> & {
    password: string;
  }): Observable<AdminUser> {
    if (stryMutAct_9fa48("231")) {
      {}
    } else {
      stryCov_9fa48("231");
      return this.http.post<AdminUser>(stryMutAct_9fa48("232") ? `` : (stryCov_9fa48("232"), `${this.baseUrl}/users`), data).pipe(catchError(this.handleError));
    }
  }
  updateUser(id: string, data: Partial<AdminUser>): Observable<AdminUser> {
    if (stryMutAct_9fa48("233")) {
      {}
    } else {
      stryCov_9fa48("233");
      return this.http.put<AdminUser>(stryMutAct_9fa48("234") ? `` : (stryCov_9fa48("234"), `${this.baseUrl}/users/${id}`), data).pipe(catchError(this.handleError));
    }
  }
  deactivateUser(id: string): Observable<void> {
    if (stryMutAct_9fa48("235")) {
      {}
    } else {
      stryCov_9fa48("235");
      return this.http.post<void>(stryMutAct_9fa48("236") ? `` : (stryCov_9fa48("236"), `${this.baseUrl}/users/${id}/deactivate`), {}).pipe(catchError(this.handleError));
    }
  }
  activateUser(id: string): Observable<void> {
    if (stryMutAct_9fa48("237")) {
      {}
    } else {
      stryCov_9fa48("237");
      return this.http.post<void>(stryMutAct_9fa48("238") ? `` : (stryCov_9fa48("238"), `${this.baseUrl}/users/${id}/activate`), {}).pipe(catchError(this.handleError));
    }
  }
  assignRoles(userId: string, roleIds: string[]): Observable<AdminUser> {
    if (stryMutAct_9fa48("239")) {
      {}
    } else {
      stryCov_9fa48("239");
      return this.http.post<AdminUser>(stryMutAct_9fa48("240") ? `` : (stryCov_9fa48("240"), `${this.baseUrl}/users/${userId}/roles`), stryMutAct_9fa48("241") ? {} : (stryCov_9fa48("241"), {
        roleIds
      })).pipe(catchError(this.handleError));
    }
  }

  // ─── Roles ──────────────────────────────────────────────────────────────────

  getRoles(): Observable<Role[]> {
    if (stryMutAct_9fa48("242")) {
      {}
    } else {
      stryCov_9fa48("242");
      return this.http.get<Role[]>(stryMutAct_9fa48("243") ? `` : (stryCov_9fa48("243"), `${this.baseUrl}/roles`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  getRole(id: string): Observable<Role> {
    if (stryMutAct_9fa48("244")) {
      {}
    } else {
      stryCov_9fa48("244");
      return this.http.get<Role>(stryMutAct_9fa48("245") ? `` : (stryCov_9fa48("245"), `${this.baseUrl}/roles/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createRole(data: {
    name: string;
    description: string;
    permissions: string[];
  }): Observable<Role> {
    if (stryMutAct_9fa48("246")) {
      {}
    } else {
      stryCov_9fa48("246");
      return this.http.post<Role>(stryMutAct_9fa48("247") ? `` : (stryCov_9fa48("247"), `${this.baseUrl}/roles`), data).pipe(catchError(this.handleError));
    }
  }
  updateRole(id: string, data: {
    name: string;
    description: string;
    permissions: string[];
  }): Observable<Role> {
    if (stryMutAct_9fa48("248")) {
      {}
    } else {
      stryCov_9fa48("248");
      return this.http.put<Role>(stryMutAct_9fa48("249") ? `` : (stryCov_9fa48("249"), `${this.baseUrl}/roles/${id}`), data).pipe(catchError(this.handleError));
    }
  }
  deleteRole(id: string): Observable<void> {
    if (stryMutAct_9fa48("250")) {
      {}
    } else {
      stryCov_9fa48("250");
      return this.http.delete<void>(stryMutAct_9fa48("251") ? `` : (stryCov_9fa48("251"), `${this.baseUrl}/roles/${id}`)).pipe(catchError(this.handleError));
    }
  }
  getPermissions(): Observable<PermissionGroup[]> {
    if (stryMutAct_9fa48("252")) {
      {}
    } else {
      stryCov_9fa48("252");
      return this.http.get<PermissionGroup[]>(stryMutAct_9fa48("253") ? `` : (stryCov_9fa48("253"), `${this.baseUrl}/permissions`)).pipe(retry(1), catchError(this.handleError));
    }
  }

  // ─── Settings ───────────────────────────────────────────────────────────────

  getSettings(): Observable<Setting[]> {
    if (stryMutAct_9fa48("254")) {
      {}
    } else {
      stryCov_9fa48("254");
      return this.http.get<Setting[]>(stryMutAct_9fa48("255") ? `` : (stryCov_9fa48("255"), `${this.baseUrl}/settings`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  getSetting(key: string): Observable<Setting> {
    if (stryMutAct_9fa48("256")) {
      {}
    } else {
      stryCov_9fa48("256");
      return this.http.get<Setting>(stryMutAct_9fa48("257") ? `` : (stryCov_9fa48("257"), `${this.baseUrl}/settings/${key}`)).pipe(catchError(this.handleError));
    }
  }
  updateSetting(key: string, value: any): Observable<Setting> {
    if (stryMutAct_9fa48("258")) {
      {}
    } else {
      stryCov_9fa48("258");
      return this.http.put<Setting>(stryMutAct_9fa48("259") ? `` : (stryCov_9fa48("259"), `${this.baseUrl}/settings/${key}`), stryMutAct_9fa48("260") ? {} : (stryCov_9fa48("260"), {
        value
      })).pipe(catchError(this.handleError));
    }
  }
  bulkUpdateSettings(data: {
    key: string;
    value: any;
  }[]): Observable<void> {
    if (stryMutAct_9fa48("261")) {
      {}
    } else {
      stryCov_9fa48("261");
      return this.http.put<void>(stryMutAct_9fa48("262") ? `` : (stryCov_9fa48("262"), `${this.baseUrl}/settings/bulk`), stryMutAct_9fa48("263") ? {} : (stryCov_9fa48("263"), {
        settings: data
      })).pipe(catchError(this.handleError));
    }
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
    if (stryMutAct_9fa48("264")) {
      {}
    } else {
      stryCov_9fa48("264");
      return this.http.get<PagedResult<AuditLog>>(stryMutAct_9fa48("265") ? `` : (stryCov_9fa48("265"), `${this.baseUrl}/audit-logs`), stryMutAct_9fa48("266") ? {} : (stryCov_9fa48("266"), {
        params: params as any
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getAuditLog(id: string): Observable<AuditLog> {
    if (stryMutAct_9fa48("267")) {
      {}
    } else {
      stryCov_9fa48("267");
      return this.http.get<AuditLog>(stryMutAct_9fa48("268") ? `` : (stryCov_9fa48("268"), `${this.baseUrl}/audit-logs/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }

  // ─── Dashboard ──────────────────────────────────────────────────────────────

  getDashboardStats(): Observable<AdminDashboardStats> {
    if (stryMutAct_9fa48("269")) {
      {}
    } else {
      stryCov_9fa48("269");
      return this.http.get<AdminDashboardStats>(stryMutAct_9fa48("270") ? `` : (stryCov_9fa48("270"), `${this.baseUrl}/dashboard`)).pipe(retry(1), catchError(this.handleError));
    }
  }

  // ─── Error Handler ──────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("271")) {
      {}
    } else {
      stryCov_9fa48("271");
      let errorMessage = stryMutAct_9fa48("272") ? "" : (stryCov_9fa48("272"), 'Lỗi không xác định');
      if (stryMutAct_9fa48("274") ? false : stryMutAct_9fa48("273") ? true : (stryCov_9fa48("273", "274"), error.error instanceof ErrorEvent)) {
        if (stryMutAct_9fa48("275")) {
          {}
        } else {
          stryCov_9fa48("275");
          errorMessage = stryMutAct_9fa48("276") ? `` : (stryCov_9fa48("276"), `Lỗi máy khách: ${error.error.message}`);
        }
      } else {
        if (stryMutAct_9fa48("277")) {
          {}
        } else {
          stryCov_9fa48("277");
          errorMessage = stryMutAct_9fa48("278") ? `` : (stryCov_9fa48("278"), `Lỗi máy chủ: ${error.status} - ${error.message}`);
        }
      }
      console.error(stryMutAct_9fa48("279") ? "" : (stryCov_9fa48("279"), '[AdminService]'), errorMessage);
      return throwError(stryMutAct_9fa48("280") ? () => undefined : (stryCov_9fa48("280"), () => error));
    }
  }
}