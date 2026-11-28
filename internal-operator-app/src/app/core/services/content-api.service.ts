import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  HisHopeContentArticleDto,
  HisHopeContentListResponse,
  HisHopeContentMediaAssetDto,
  HisHopePartnershipInquiryDto,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

export interface UpsertArticleRequest {
  slug: string;
  title: string;
  excerpt: string;
  bodyHtml: string;
  category: string;
  imageUrl: string;
  locale: string;
  status: string;
  seoTitle?: string | null;
  seoDescription?: string | null;
  seoKeywords?: string | null;
  publishedAt?: string | null;
}

@Injectable({ providedIn: "root" })
export class ContentApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.contentApiUrl;

  getArticles(locale?: string) {
    const params = locale ? { locale } : undefined;
    return this.http.get<HisHopeContentListResponse<HisHopeContentArticleDto>>(
      `${this.base}/articles`,
      { params },
    );
  }

  createArticle(body: UpsertArticleRequest) {
    return this.http.post<HisHopeContentArticleDto>(`${this.base}/articles`, body);
  }

  updateArticle(articleId: string, body: UpsertArticleRequest) {
    return this.http.put<HisHopeContentArticleDto>(`${this.base}/articles/${articleId}`, body);
  }

  deleteArticle(articleId: string) {
    return this.http.delete<void>(`${this.base}/articles/${articleId}`);
  }

  getInquiries() {
    return this.http.get<HisHopeContentListResponse<HisHopePartnershipInquiryDto>>(
      `${this.base}/inquiries`,
    );
  }

  updateInquiryStatus(inquiryId: string, status: string) {
    return this.http.patch<HisHopePartnershipInquiryDto>(
      `${this.base}/inquiries/${inquiryId}/status`,
      { status },
    );
  }

  getMedia() {
    return this.http.get<HisHopeContentListResponse<HisHopeContentMediaAssetDto>>(
      `${this.base}/media`,
    );
  }

  uploadMedia(file: File) {
    const formData = new FormData();
    formData.append("file", file);
    return this.http.post<HisHopeContentMediaAssetDto>(`${this.base}/media/upload`, formData);
  }
}
