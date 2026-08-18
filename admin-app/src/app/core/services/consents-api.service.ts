import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  AdminPageQuery,
  AdminPageResult,
  Consent,
} from "../contracts/admin.contracts";
import { buildAdminPageParams } from "./admin-query.util";

@Injectable({ providedIn: "root" })
export class ConsentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getConsentsPage(query: AdminPageQuery): Observable<AdminPageResult<Consent>> {
    const params = buildAdminPageParams(query);
    return this.http.get<AdminPageResult<Consent>>(`${this.baseUrl}/consents`, {
      params,
    });
  }
}
