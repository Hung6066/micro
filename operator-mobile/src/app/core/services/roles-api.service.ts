import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { HisHopeBulkActionRequest } from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import {
  MobileBulkActionResponse,
  MobilePageQuery,
  MobilePageResult,
  PermissionDefinition,
  Role,
  RoleOwnerOption,
} from "../contracts/mobile.contracts";
import { MobileTableApiService } from "./mobile-table-api.service";
import { buildMobilePageParams, mobilePageCacheKey } from "./mobile-query.util";

@Injectable({ providedIn: "root" })
export class RolesApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(MobileTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getRolesPage(query: MobilePageQuery): Observable<MobilePageResult<Role>> {
    const params = buildMobilePageParams(query);
    return this.requestCache.getOrLoad(mobilePageCacheKey("roles", params), () =>
      this.http.get<MobilePageResult<Role>>(`${this.baseUrl}/roles`, {
        params,
      }),
    );
  }

  getRole(id: string): Observable<Role> {
    return this.http.get<Role>(
      `${this.baseUrl}/roles/${encodeURIComponent(id)}`,
    );
  }

  bulkRoles(
    request: HisHopeBulkActionRequest,
  ): Observable<MobileBulkActionResponse> {
    return this.tableApi.bulk("roles", request);
  }

  createRole(role: Partial<Role> & { permissions?: string[] }): Observable<Role> {
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

  getPermissions(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(
      `${this.baseUrl}/permissions`,
    );
  }

  getRoleOwners(): Observable<RoleOwnerOption[]> {
    return this.http.get<RoleOwnerOption[]>(`${this.baseUrl}/role-owners`);
  }
}
