/** Mirrors ContentService DTOs (camelCase JSON). */

export interface HisHopeContentBannerDto {
  id: string;
  tenantKey: string;
  slideKey: string;
  imageUrl: string;
  eyebrowKey: string;
  titleKey: string;
  subtitleKey: string;
  sortOrder: number;
  isPublished: boolean;
}

export interface HisHopeContentStoryBlockDto {
  id: string;
  tenantKey: string;
  blockKey: string;
  titleKey: string;
  bodyKey: string;
  tagKey: string;
  imageUrl: string;
  sortOrder: number;
  isPublished: boolean;
}

export interface HisHopeContentArticleDto {
  id: string;
  tenantKey: string;
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
  publishedAt: string;
  updatedAt: string;
}

export interface HisHopeContentHomeDto {
  banners: HisHopeContentBannerDto[];
  stories: HisHopeContentStoryBlockDto[];
  articles: HisHopeContentArticleDto[];
  founderStory?: HisHopeContentArticleDto | null;
}

export interface HisHopeContentListResponse<T> {
  items: T[];
}

export interface HisHopePartnershipInquiryDto {
  id: string;
  tenantKey: string;
  companyName: string;
  contactName: string;
  email: string;
  phone: string;
  partnershipType: string;
  message: string;
  status: string;
  createdAt: string;
}

export interface HisHopeCreatePartnershipInquiryRequest {
  companyName: string;
  contactName: string;
  email: string;
  phone: string;
  partnershipType: string;
  message: string;
}

export interface HisHopeContentMediaAssetDto {
  id: string;
  tenantKey: string;
  fileName: string;
  contentType: string;
  publicUrl: string;
  sizeBytes: number;
  uploadedAt: string;
}
