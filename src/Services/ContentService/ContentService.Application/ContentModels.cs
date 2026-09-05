namespace His.Hope.ContentService.Application;

public sealed record BannerDto(
    Guid Id,
    string TenantKey,
    string SlideKey,
    string ImageUrl,
    string EyebrowKey,
    string TitleKey,
    string SubtitleKey,
    int SortOrder,
    bool IsPublished);

public sealed record StoryBlockDto(
    Guid Id,
    string TenantKey,
    string BlockKey,
    string TitleKey,
    string BodyKey,
    string TagKey,
    string ImageUrl,
    int SortOrder,
    bool IsPublished);

public sealed record ArticleDto(
    Guid Id,
    string TenantKey,
    string Slug,
    string Title,
    string Excerpt,
    string BodyHtml,
    string Category,
    string ImageUrl,
    string Locale,
    string Status,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt);

public sealed record PartnershipInquiryDto(
    Guid Id,
    string TenantKey,
    string CompanyName,
    string ContactName,
    string Email,
    string Phone,
    string PartnershipType,
    string Message,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record MediaAssetDto(
    Guid Id,
    string TenantKey,
    string FileName,
    string ContentType,
    string PublicUrl,
    long SizeBytes,
    DateTimeOffset UploadedAt);

public sealed record NewsletterSubscriptionDto(
    Guid Id,
    string TenantKey,
    string Email,
    DateTimeOffset SubscribedAt);

public sealed record HomeContentDto(
    IReadOnlyList<BannerDto> Banners,
    IReadOnlyList<StoryBlockDto> Stories,
    IReadOnlyList<ArticleDto> Articles,
    ArticleDto? FounderStory);

public sealed record UpsertBannerRequest(
    string SlideKey,
    string ImageUrl,
    string EyebrowKey,
    string TitleKey,
    string SubtitleKey,
    int SortOrder,
    bool IsPublished);

public sealed record UpsertStoryBlockRequest(
    string BlockKey,
    string TitleKey,
    string BodyKey,
    string TagKey,
    string ImageUrl,
    int SortOrder,
    bool IsPublished);

public sealed record UpsertArticleRequest(
    string Slug,
    string Title,
    string Excerpt,
    string BodyHtml,
    string Category,
    string ImageUrl,
    string Locale,
    string Status,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    DateTimeOffset? PublishedAt);

public sealed record CreatePartnershipInquiryRequest(
    string CompanyName,
    string ContactName,
    string Email,
    string Phone,
    string PartnershipType,
    string Message);

public sealed record UpdateInquiryStatusRequest(string Status);

public sealed record SubscribeNewsletterRequest(string Email);
