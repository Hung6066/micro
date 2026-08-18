import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map, tap } from "rxjs";
import {
  AdminPageQuery,
  AdminPageResult,
  AdminBulkActionResponse,
  AdminTableView,
  User,
} from "../contracts/admin.contracts";
import { AdminTableApiService } from "./admin-table-api.service";
import {
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import { adminPageCacheKey, buildAdminPageParams } from "./admin-query.util";

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  licenseNumber?: string;
  specialty?: string;
  phoneNumber?: string;
  role?: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  role?: string;
  isActive?: boolean;
  concurrencyToken?: string;
}

/** Workforce identity bounded-context API. */
@Injectable({ providedIn: "root" })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(AdminTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getUsersPage(query: AdminPageQuery): Observable<AdminPageResult<User>> {
    const params = buildAdminPageParams(query);
    return this.requestCache.getOrLoad(adminPageCacheKey("users", params), () =>
      this.http.get<AdminPageResult<User>>(`${this.baseUrl}/users`, {
        params,
      }),
    );
  }

  getUsers(): Observable<User[]> {
    return this.getUsersPage({ page: 1, pageSize: 100 }).pipe(
      map((response) => response.items),
    );
  }

  getUser(id: string): Observable<User> {
    return this.http.get<User>(
      `${this.baseUrl}/users/${encodeURIComponent(id)}`,
    );
  }

  bulkUsers(
    request: HisHopeBulkActionRequest,
  ): Observable<AdminBulkActionResponse> {
    return this.tableApi.bulk("users", request);
  }

  createUser(request: CreateUserRequest): Observable<User> {
    return this.http
      .post<User>(`${this.baseUrl}/users`, request)
      .pipe(tap(() => this.requestCache.invalidate("admin:users:")));
  }

  updateUser(id: string, request: UpdateUserRequest): Observable<User> {
    return this.http
      .put<User>(`${this.baseUrl}/users/${encodeURIComponent(id)}`, request)
      .pipe(tap(() => this.requestCache.invalidate("admin:users:")));
  }

  getTableViews(_resource: "users"): Observable<AdminTableView[]> {
    return this.tableApi.getViews("users");
  }

  saveTableView(
    _resource: "users",
    name: string,
    payload: unknown,
  ): Observable<AdminTableView> {
    return this.tableApi.saveView("users", name, payload);
  }

  deleteTableView(_resource: "users", name: string): Observable<void> {
    return this.tableApi.deleteView("users", name);
  }

  exportTable(
    _resource: "users",
    request: HisHopeTableExportRequest,
  ): Observable<Blob> {
    return this.tableApi.export("users", request);
  }

  activateUser(id: string): Observable<void> {
    return this.setActive(id, true);
  }

  deactivateUser(id: string): Observable<void> {
    return this.setActive(id, false);
  }

  private setActive(id: string, active: boolean): Observable<void> {
    const action = active ? "activate" : "deactivate";
    return this.http
      .put<void>(
        `${this.baseUrl}/users/${encodeURIComponent(id)}/${action}`,
        {},
      )
      .pipe(tap(() => this.requestCache.invalidate("admin:users:")));
  }
}
