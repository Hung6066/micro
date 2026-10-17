import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import {
  HisHopeContentArticleDto,
  HisHopeContentHomeDto,
  HisHopeContentListResponse,
  HisHopeCreatePartnershipInquiryRequest,
  HisHopePartnershipInquiryDto,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

/** The buyer portal is provisioned for one customer tenant; keep this identity
 * in one place and always send it as canonical request context. */
export const BUYER_TENANT_KEY = "customer-factory-x";

@Injectable({ providedIn: "root" })
export class ContentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.contentApiUrl;

  private tenantHeaders() {
    return new HttpHeaders({ "X-HisHope-Tenant": BUYER_TENANT_KEY });
  }

  getHome(locale?: string) {
    let params = new HttpParams();
    if (locale) params = params.set("locale", locale);
    return this.http.get<HisHopeContentHomeDto>(`${this.base}/public/home`, {
      params,
      headers: this.tenantHeaders(),
    });
  }

  getArticles(locale?: string) {
    let params = new HttpParams();
    if (locale) params = params.set("locale", locale);
    return this.http.get<HisHopeContentListResponse<HisHopeContentArticleDto>>(
      `${this.base}/public/articles`,
      { params, headers: this.tenantHeaders() },
    );
  }

  getArticle(slug: string) {
    return this.http.get<HisHopeContentArticleDto>(
      `${this.base}/public/articles/${encodeURIComponent(slug)}`,
      {
        headers: this.tenantHeaders(),
      },
    );
  }

  submitPartnershipInquiry(body: HisHopeCreatePartnershipInquiryRequest) {
    return this.http.post<HisHopePartnershipInquiryDto>(
      `${this.base}/public/partnership-inquiries`,
      body,
      { headers: this.tenantHeaders() },
    );
  }

  subscribeNewsletter(email: string) {
    return this.http.post<{ id: string; email: string }>(
      `${this.base}/public/newsletter/subscriptions`,
      { email },
      { headers: this.tenantHeaders() },
    );
  }
}
