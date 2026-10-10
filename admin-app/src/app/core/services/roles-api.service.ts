import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map } from "rxjs";
import {
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import {
  AdminBulkActionResponse,
  AdminPageQuery,
  AdminPageResult,
  AdminTableView,
  PermissionDefinition,
  Role,
  RoleOwnerOption,
} from "../contracts/admin.contracts";
import { AdminTableApiService } from "./admin-table-api.service";
import { adminPageCacheKey, buildAdminPageParams } from "./admin-query.util";

@Injectable({ providedIn: "root" })
export class RolesApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(AdminTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getRolesPage(query: AdminPageQuery): Observable<AdminPageResult<Role>> {
    const params = buildAdminPageParams(query);
    return this.requestCache.getOrLoad(adminPageCacheKey("roles", params), () =>
      this.http.get<AdminPageResult<Role>>(`${this.baseUrl}/roles`, {
        params,
      }),
    );
  }

  bulkRoles(
    request: HisHopeBulkActionRequest,
  ): Observable<AdminBulkActionResponse> {
    return this.tableApi.bulk("roles", request);
  }

  exportTable(
    _resource: "roles",
    request: HisHopeTableExportRequest,
  ): Observable<Blob> {
    return this.tableApi.export("roles", request);
  }

  getTableViews(_resource: "roles"): Observable<AdminTableView[]> {
    void _resource;
    return this.tableApi.getViews("roles");
  }

  saveTableView(
    _resource: "roles",
    name: string,
    payload: unknown,
  ): Observable<AdminTableView> {
    return this.tableApi.saveView("roles", name, payload);
  }

  deleteTableView(_resource: "roles", name: string): Observable<void> {
    return this.tableApi.deleteView("roles", name);
  }

  getRoles(): Observable<Role[]> {
    return this.getRolesPage({ page: 1, pageSize: 100 }).pipe(
      map((response) => response.items),
    );
  }

  createRole(role: Partial<Role>): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/roles`, role);
  }

  updateRole(
    id: string,
    role: Partial<Role> & { permissions?: string[] },
  ): Observable<Role> {
    return this.http.put<Role>(
      `${this.baseUrl}/roles/${encodeURIComponent(id)}`,
      role,
    );
  }

  publishRole(id: string): Observable<RoleMutationResponse> {
    return this.http.post<RoleMutationResponse>(
      `${this.baseUrl}/roles/${encodeURIComponent(id)}/publish`,
      {},
    );
  }

  rollbackRole(id: string): Observable<RoleMutationResponse> {
    return this.http.post<RoleMutationResponse>(
      `${this.baseUrl}/roles/${encodeURIComponent(id)}/rollback`,
      {},
    );
  }

  getPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`);
  }

  getRoleOwners(): Observable<RoleOwnerOption[]> {
    return this.http.get<RoleOwnerOption[]>(`${this.baseUrl}/role-owners`);
  }

  /* Kept as a named contract so 202 approval requests are not mistaken for
     an executed role mutation by callers. */
}

export interface RoleMutationResponse {
  id?: string;
  authorizationVersion?: number;
  lifecycleStatus?: string;
  publishedAt?: string;
  restoredFromVersion?: number;
  changeRequestId?: string;
  status?: string;
  expiresAt?: string;
}
