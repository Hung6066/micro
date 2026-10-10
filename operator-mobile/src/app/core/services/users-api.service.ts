import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, tap } from "rxjs";
import { HisHopeBulkActionRequest } from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import {
  CreateUserRequest,
  MobileBulkActionResponse,
  MobilePageQuery,
  MobilePageResult,
  UpdateUserRequest,
  User,
} from "../contracts/mobile.contracts";
import { MobileTableApiService } from "./mobile-table-api.service";
import { buildMobilePageParams, mobilePageCacheKey } from "./mobile-query.util";

@Injectable({ providedIn: "root" })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(MobileTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getUsersPage(query: MobilePageQuery): Observable<MobilePageResult<User>> {
    const params = buildMobilePageParams(query);
    return this.requestCache.getOrLoad(mobilePageCacheKey("users", params), () =>
      this.http.get<MobilePageResult<User>>(`${this.baseUrl}/users`, {
        params,
      }),
    );
  }

  getUser(id: string): Observable<User> {
    return this.http.get<User>(
      `${this.baseUrl}/users/${encodeURIComponent(id)}`,
    );
  }

  bulkUsers(
    request: HisHopeBulkActionRequest,
  ): Observable<MobileBulkActionResponse> {
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
}
