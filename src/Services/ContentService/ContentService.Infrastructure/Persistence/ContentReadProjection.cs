using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ContentService.Infrastructure;

public sealed partial class PostgresContentStore
{
    public HomeContentDto GetHome(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var banners = db.Banners.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.IsPublished)
            .OrderBy(x => x.SortOrder).ToList().Select(ToBannerDto).ToArray();
        var stories = db.StoryBlocks.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.IsPublished)
            .OrderBy(x => x.SortOrder).ToList().Select(ToStoryDto).ToArray();
        var articles = db.Articles.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Status == ContentArticleStatuses.Published)
            .OrderByDescending(x => x.PublishedAt).Take(3).ToList().Select(ToArticleDto).ToArray();
        var founder = db.Articles.AsNoTracking()
            .FirstOrDefault(x => x.TenantKey == tenantKey && x.Slug == "founder-story" && x.Status == ContentArticleStatuses.Published);
        return new HomeContentDto(banners, stories, articles, founder is null ? null : ToArticleDto(founder));
    }

    public IReadOnlyList<BannerDto> ListBanners(string tenantKey, bool publishedOnly)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Banners.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (publishedOnly) query = query.Where(x => x.IsPublished);
        return query.OrderBy(x => x.SortOrder).ToList().Select(ToBannerDto).ToArray();
    }

    public BannerDto? GetBanner(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Banners.AsNoTracking().FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        return entity is null ? null : ToBannerDto(entity);
    }

    public IReadOnlyList<StoryBlockDto> ListStories(string tenantKey, bool publishedOnly)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.StoryBlocks.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (publishedOnly) query = query.Where(x => x.IsPublished);
        return query.OrderBy(x => x.SortOrder).ToList().Select(ToStoryDto).ToArray();
    }

    public StoryBlockDto? GetStory(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.StoryBlocks.AsNoTracking().FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        return entity is null ? null : ToStoryDto(entity);
    }

    public IReadOnlyList<ArticleDto> ListArticles(string tenantKey, bool publishedOnly, string? locale, int? take = null)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Articles.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (publishedOnly) query = query.Where(x => x.Status == ContentArticleStatuses.Published);
        if (!string.IsNullOrWhiteSpace(locale)) query = query.Where(x => x.Locale == locale);
        query = query.OrderByDescending(x => x.PublishedAt);
        if (take is > 0) query = query.Take(take.Value);
        return query.ToList().Select(ToArticleDto).ToArray();
    }

    public ArticleDto? GetArticleBySlug(string tenantKey, string slug)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Articles.AsNoTracking().FirstOrDefault(x => x.TenantKey == tenantKey && x.Slug == slug);
        return entity is null ? null : ToArticleDto(entity);
    }

    public ArticleDto? GetArticle(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Articles.AsNoTracking().FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        return entity is null ? null : ToArticleDto(entity);
    }
}
