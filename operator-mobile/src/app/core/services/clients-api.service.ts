import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map, tap } from "rxjs";
import { HisHopeBulkActionRequest } from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import {
  ClientSecretResponse,
  MobileBulkActionResponse,
  MobilePageQuery,
  MobilePageResult,
  OidcClient,
} from "../contracts/mobile.contracts";
import { MobileTableApiService } from "./mobile-table-api.service";
import { buildMobilePageParams, mobilePageCacheKey } from "./mobile-query.util";

@Injectable({ providedIn: "root" })
export class ClientsApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(MobileTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getClientsPage(
    query: MobilePageQuery,
  ): Observable<MobilePageResult<OidcClient>> {
    const params = buildMobilePageParams(query);
    return this.requestCache
      .getOrLoad(mobilePageCacheKey("clients", params), () =>
        this.http.get<MobilePageResult<OidcClient & { type?: string }>>(
          `${this.baseUrl}/clients`,
          { params },
        ),
      )
      .pipe(
        map((response) => ({
          ...response,
          items: response.items.map((client) => ({
            ...client,
            clientType: client.clientType ?? client.type ?? "",
          })),
        })),
      );
  }

  getClient(id: string): Observable<OidcClient> {
    return this.http.get<OidcClient>(`${this.baseUrl}/clients/${id}`);
  }

  createClient(client: Partial<OidcClient>): Observable<ClientSecretResponse> {
    return this.http
      .post<ClientSecretResponse>(`${this.baseUrl}/clients`, client)
      .pipe(tap(() => this.requestCache.invalidate("admin:clients:")));
  }

  updateClient(
    id: string,
    client: Partial<OidcClient>,
  ): Observable<OidcClient> {
    return this.http
      .put<OidcClient>(`${this.baseUrl}/clients/${id}`, client)
      .pipe(tap(() => this.requestCache.invalidate("admin:clients:")));
  }

  bulkClients(
    request: HisHopeBulkActionRequest,
  ): Observable<MobileBulkActionResponse> {
    return this.tableApi.bulk("clients", request);
  }
}
