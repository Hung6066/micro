import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  AdminPageQuery,
  MobileDeliverySummary,
  MobileDeviceRegistration,
} from "../contracts/admin.contracts";
import { buildAdminPageParams } from "./admin-query.util";

@Injectable({ providedIn: "root" })
export class MobileOperationsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getMobileDevices(
    query: AdminPageQuery = { page: 1, pageSize: 50 },
  ): Observable<{
    items: MobileDeviceRegistration[];
    page: number;
    pageSize: number;
    total: number;
  }> {
    const params = buildAdminPageParams(query);
    return this.http.get<{
      items: MobileDeviceRegistration[];
      page: number;
      pageSize: number;
      total: number;
    }>(`${this.baseUrl}/mobile/devices`, { params });
  }

  revokeMobileDevice(id: string): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/mobile/devices/${encodeURIComponent(id)}/revoke`,
      {},
    );
  }

  getMobileDeliverySummary(hours = 24): Observable<MobileDeliverySummary> {
    return this.http.get<MobileDeliverySummary>(
      `${this.baseUrl}/push/delivery-summary`,
      { params: { hours } },
    );
  }
}
