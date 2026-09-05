using System.Text.Json;
using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using His.Hope.Contracts.Saga;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ContentService.Infrastructure;

public sealed partial class PostgresContentStore
{
    public BannerDto UpsertBanner(Guid? id, string tenantKey, UpsertBannerRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = id is null
            ? new ContentBannerEntity { Id = Guid.NewGuid(), TenantKey = tenantKey }
            : db.Banners.First(x => x.Id == id && x.TenantKey == tenantKey);

        entity.SlideKey = request.SlideKey.Trim();
        entity.ImageUrl = request.ImageUrl.Trim();
        entity.EyebrowKey = request.EyebrowKey.Trim();
        entity.TitleKey = request.TitleKey.Trim();
        entity.SubtitleKey = request.SubtitleKey.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsPublished = request.IsPublished;

        if (id is null) db.Banners.Add(entity);
        db.SaveChanges();
        return ToBannerDto(entity);
    }

    public bool DeleteBanner(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Banners.FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        if (entity is null) return false;
        db.Banners.Remove(entity);
        db.SaveChanges();
        return true;
    }

    public StoryBlockDto UpsertStory(Guid? id, string tenantKey, UpsertStoryBlockRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = id is null
            ? new ContentStoryBlockEntity { Id = Guid.NewGuid(), TenantKey = tenantKey }
            : db.StoryBlocks.First(x => x.Id == id && x.TenantKey == tenantKey);

        entity.BlockKey = request.BlockKey.Trim();
        entity.TitleKey = request.TitleKey.Trim();
        entity.BodyKey = request.BodyKey.Trim();
        entity.TagKey = request.TagKey.Trim();
        entity.ImageUrl = request.ImageUrl.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsPublished = request.IsPublished;

        if (id is null) db.StoryBlocks.Add(entity);
        db.SaveChanges();
        return ToStoryDto(entity);
    }

    public bool DeleteStory(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.StoryBlocks.FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        if (entity is null) return false;
        db.StoryBlocks.Remove(entity);
        db.SaveChanges();
        return true;
    }

    public ArticleDto UpsertArticle(Guid? id, string tenantKey, UpsertArticleRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var entity = id is null
            ? new ContentArticleEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, PublishedAt = request.PublishedAt ?? now, UpdatedAt = now }
            : db.Articles.First(x => x.Id == id && x.TenantKey == tenantKey);
        var wasPublished = entity.Status == ContentArticleStatuses.Published;

        entity.Slug = request.Slug.Trim().ToLowerInvariant();
        entity.Title = request.Title.Trim();
        entity.Excerpt = request.Excerpt.Trim();
        entity.BodyHtml = request.BodyHtml.Trim();
        entity.Category = request.Category.Trim();
        entity.ImageUrl = request.ImageUrl.Trim();
        entity.Locale = request.Locale.Trim();
        entity.Status = request.Status.Trim().ToLowerInvariant();
        entity.SeoTitle = request.SeoTitle?.Trim();
        entity.SeoDescription = request.SeoDescription?.Trim();
        entity.SeoKeywords = request.SeoKeywords?.Trim();
        entity.UpdatedAt = now;
        if (request.PublishedAt is not null) entity.PublishedAt = request.PublishedAt.Value;
        else if (entity.Status == ContentArticleStatuses.Published && !wasPublished) entity.PublishedAt = now;

        if (id is null) db.Articles.Add(entity);
        if (entity.Status == ContentArticleStatuses.Published && !wasPublished)
        {
            var published = new ContentPublishedV1(Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion,
                now, entity.Id, entity.TenantKey, entity.Locale, $"content-publish:{entity.TenantKey}:{entity.Id}:{entity.UpdatedAt.Ticks}");
            db.PublishingOutbox.Add(new ContentPublishingOutboxEntity
            {
                Id = Guid.NewGuid(), Type = SagaMessagingContract.ContentPublished,
                Content = JsonSerializer.Serialize(published), OccurredAt = now
            });
            db.PublishingOutbox.Add(new ContentPublishingOutboxEntity
            {
                Id = Guid.NewGuid(), Type = SagaMessagingContract.ContentNotificationRequested,
                Content = JsonSerializer.Serialize(new ContentNotificationRequestedV1(published.EventId,
                    published.SchemaVersion, published.OccurredAt, published.ArticleId, published.TenantKey,
                    published.Locale, published.IdempotencyKey, published.CorrelationId, published.CausationId)), OccurredAt = now
            });
        }
        db.SaveChanges();
        return ToArticleDto(entity);
    }

    public bool DeleteArticle(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Articles.FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        if (entity is null) return false;
        db.Articles.Remove(entity);
        db.SaveChanges();
        return true;
    }
}
