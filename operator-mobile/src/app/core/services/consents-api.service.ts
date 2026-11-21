import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  Consent,
  MobilePageQuery,
  MobilePageResult,
} from "../contracts/mobile.contracts";
import { buildMobilePageParams } from "./mobile-query.util";

@Injectable({ providedIn: "root" })
export class ConsentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getConsentsPage(
    query: MobilePageQuery,
  ): Observable<MobilePageResult<Consent>> {
    const params = buildMobilePageParams(query);
    return this.http.get<MobilePageResult<Consent>>(
      `${this.baseUrl}/consents`,
      { params },
    );
  }

  /**
   * Identity Service exposes no `/consents/{id}` endpoint, so a single record
   * is resolved by searching the paged list.
   */
  getConsent(id: string): Observable<Consent | null> {
    return this.getConsentsPage({ page: 1, pageSize: 50, search: id }).pipe(
      map((result) => result.items.find((item) => item.id === id) ?? null),
    );
  }
}
