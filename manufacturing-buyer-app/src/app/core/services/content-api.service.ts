import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpParams } from "@angular/common/http";
import {
  HisHopeContentArticleDto,
  HisHopeContentHomeDto,
  HisHopeContentListResponse,
  HisHopeCreatePartnershipInquiryRequest,
  HisHopePartnershipInquiryDto,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

const TENANT_KEY = "customer-factory-x";

@Injectable({ providedIn: "root" })
export class ContentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.contentApiUrl;

  private tenantParams() {
    return new HttpParams().set("tenantKey", TENANT_KEY);
  }

  getHome(locale?: string) {
    let params = this.tenantParams();
    if (locale) params = params.set("locale", locale);
    return this.http.get<HisHopeContentHomeDto>(`${this.base}/public/home`, {
      params,
    });
  }

  getArticles(locale?: string) {
    let params = this.tenantParams();
    if (locale) params = params.set("locale", locale);
    return this.http.get<HisHopeContentListResponse<HisHopeContentArticleDto>>(
      `${this.base}/public/articles`,
      { params },
    );
  }

  getArticle(slug: string) {
    return this.http.get<HisHopeContentArticleDto>(`${this.base}/public/articles/${encodeURIComponent(slug)}`, {
      params: this.tenantParams(),
    });
  }

  submitPartnershipInquiry(body: HisHopeCreatePartnershipInquiryRequest) {
    return this.http.post<HisHopePartnershipInquiryDto>(
      `${this.base}/public/partnership-inquiries`,
      body,
      { params: this.tenantParams() },
    );
  }

  subscribeNewsletter(email: string) {
    return this.http.post<{ id: string; email: string }>(
      `${this.base}/public/newsletter/subscriptions`,
      { email },
      { params: this.tenantParams() },
    );
  }
}
