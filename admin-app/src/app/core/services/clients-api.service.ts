import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map, tap } from "rxjs";
import {
  AdminPageQuery,
  AdminPageResult,
  AdminBulkActionResponse,
  AdminTableView,
  ClientOnboarding,
  ClientSecretResponse,
  OidcClient,
} from "../contracts/admin.contracts";
import { AdminTableApiService } from "./admin-table-api.service";
import { HisHopeTableExportRequest } from "@his-hope/frontend-foundation/contracts";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import { adminPageCacheKey, buildAdminPageParams } from "./admin-query.util";

/** Client bounded-context API. Keeps client features independent from the legacy admin facade. */
@Injectable({ providedIn: "root" })
export class ClientsApiService {
  private readonly http = inject(HttpClient);
  private readonly requestCache = inject(HisHopeRequestCacheService);
  private readonly tableApi = inject(AdminTableApiService);
  private readonly baseUrl = environment.adminApiUrl;

  getClientsPage(
    query: AdminPageQuery,
  ): Observable<AdminPageResult<OidcClient>> {
    const params = buildAdminPageParams(query);
    return this.requestCache
      .getOrLoad(adminPageCacheKey("clients", params), () =>
        this.http.get<AdminPageResult<OidcClient & { type?: string }>>(
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

  getClients(): Observable<OidcClient[]> {
    return this.getClientsPage({ page: 1, pageSize: 100 }).pipe(
      map((response) => response.items),
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

  deleteClient(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/clients/${id}`)
      .pipe(tap(() => this.requestCache.invalidate("admin:clients:")));
  }

  rotateClientSecret(id: string): Observable<ClientSecretResponse> {
    return this.http.post<ClientSecretResponse>(
      `${this.baseUrl}/clients/${id}/rotate-secret`,
      {},
    );
  }

  getClientOnboarding(id: string): Observable<ClientOnboarding> {
    return this.http.get<ClientOnboarding>(
      `${this.baseUrl}/clients/${id}/onboarding`,
    );
  }

  bulkTable(
    _resource: "clients",
    request: import("@his-hope/frontend-foundation/contracts").HisHopeBulkActionRequest,
  ): Observable<AdminBulkActionResponse> {
    return this.tableApi.bulk("clients", request);
  }

  exportTable(
    _resource: "clients",
    request: HisHopeTableExportRequest,
  ): Observable<Blob> {
    return this.tableApi.export("clients", request);
  }

  getTableViews(_resource: "clients"): Observable<AdminTableView[]> {
    void _resource;
    return this.tableApi.getViews("clients");
  }

  saveTableView(
    _resource: "clients",
    name: string,
    payload: unknown,
  ): Observable<AdminTableView> {
    return this.tableApi.saveView("clients", name, payload);
  }

  deleteTableView(_resource: "clients", name: string): Observable<void> {
    return this.tableApi.deleteView("clients", name);
  }
}
