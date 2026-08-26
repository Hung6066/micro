using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Xml.Linq;

namespace His.Hope.ContentService.Infrastructure;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<ContentBannerEntity> Banners => Set<ContentBannerEntity>();
    public DbSet<ContentStoryBlockEntity> StoryBlocks => Set<ContentStoryBlockEntity>();
    public DbSet<ContentArticleEntity> Articles => Set<ContentArticleEntity>();
    public DbSet<ContentPartnershipInquiryEntity> PartnershipInquiries => Set<ContentPartnershipInquiryEntity>();
    public DbSet<ContentMediaAssetEntity> MediaAssets => Set<ContentMediaAssetEntity>();
    public DbSet<ContentNewsletterSubscriptionEntity> NewsletterSubscriptions => Set<ContentNewsletterSubscriptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentBannerEntity>(entity =>
        {
            entity.ToTable("content_banners");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SlideKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ImageUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.EyebrowKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TitleKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SubtitleKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.SortOrder });
        });

        modelBuilder.Entity<ContentStoryBlockEntity>(entity =>
        {
            entity.ToTable("content_story_blocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BlockKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TitleKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BodyKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TagKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ImageUrl).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.SortOrder });
        });

        modelBuilder.Entity<ContentArticleEntity>(entity =>
        {
            entity.ToTable("content_articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Excerpt).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.BodyHtml).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ImageUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Locale).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SeoTitle).HasMaxLength(500);
            entity.Property(x => x.SeoDescription).HasMaxLength(2000);
            entity.Property(x => x.SeoKeywords).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantKey, x.Slug }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.Status, x.PublishedAt });
        });

        modelBuilder.Entity<ContentPartnershipInquiryEntity>(entity =>
        {
            entity.ToTable("content_partnership_inquiries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.PartnershipType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.CreatedAt });
        });

        modelBuilder.Entity<ContentMediaAssetEntity>(entity =>
        {
            entity.ToTable("content_media_assets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PublicUrl).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.UploadedAt });
        });

        modelBuilder.Entity<ContentNewsletterSubscriptionEntity>(entity =>
        {
            entity.ToTable("content_newsletter_subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Email }).IsUnique();
        });
    }
}

public sealed class ContentBannerEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string SlideKey { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string EyebrowKey { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string SubtitleKey { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class ContentStoryBlockEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string BlockKey { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string BodyKey { get; set; } = "";
    public string TagKey { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class ContentArticleEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string BodyHtml { get; set; } = "";
    public string Category { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string Locale { get; set; } = "";
    public string Status { get; set; } = ContentArticleStatuses.Draft;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ContentPartnershipInquiryEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PartnershipType { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = ContentInquiryStatuses.New;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ContentMediaAssetEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class ContentNewsletterSubscriptionEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTimeOffset SubscribedAt { get; set; }
}

public sealed class PostgresContentStore(IDbContextFactory<ContentDbContext> dbFactory)
{
    public void Initialize()
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Banners.Any())
            return;

        const string tenant = "customer-factory-x";
        var now = DateTimeOffset.UtcNow;

        db.Banners.AddRange(
            new ContentBannerEntity { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"), TenantKey = tenant, SlideKey = "story", ImageUrl = Img("photo-1622206157934-1f4720b5d0b0"), EyebrowKey = "buyer.home.hero.story.eyebrow", TitleKey = "buyer.home.hero.story.title", SubtitleKey = "buyer.home.hero.story.subtitle", SortOrder = 0, IsPublished = true },
            new ContentBannerEntity { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002"), TenantKey = tenant, SlideKey = "mango", ImageUrl = Img("photo-1605027990126-548a374fe831"), EyebrowKey = "buyer.home.hero.mango.eyebrow", TitleKey = "buyer.home.hero.mango.title", SubtitleKey = "buyer.home.hero.mango.subtitle", SortOrder = 1, IsPublished = true },
            new ContentBannerEntity { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003"), TenantKey = tenant, SlideKey = "process", ImageUrl = Img("photo-1464226184884-fa280b87eda0"), EyebrowKey = "buyer.home.hero.process.eyebrow", TitleKey = "buyer.home.hero.process.title", SubtitleKey = "buyer.home.hero.process.subtitle", SortOrder = 2, IsPublished = true });

        var storyKeys = new[] { "xoai", "thom", "chanh-day", "mix", "tac", "chom" };
        var storyImages = new[] { Img("photo-1605027990126-548a374fe831"), Img("photo-1587049350793-b760d7b16036"), Img("photo-1615485290624-6c1f5a1a2ae4"), Img("photo-1610837125200-a876848f7f9b7"), Img("photo-1587735246450-1d7d43f7f9b7"), Img("photo-1595475203575-5d2716c8c6c3") };
        for (var i = 0; i < storyKeys.Length; i++)
        {
            var key = storyKeys[i];
            db.StoryBlocks.Add(new ContentStoryBlockEntity
            {
                Id = Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb{i + 1:D4}"),
                TenantKey = tenant,
                BlockKey = key,
                TitleKey = $"buyer.home.story.{MapStoryPrefix(key)}.title",
                BodyKey = $"buyer.home.story.{MapStoryPrefix(key)}.body",
                TagKey = $"buyer.home.story.{MapStoryPrefix(key)}.tag",
                ImageUrl = storyImages[i],
                SortOrder = i,
                IsPublished = true,
            });
        }

        db.Articles.AddRange(
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0001"), TenantKey = tenant, Slug = "founder-story", Title = "Câu chuyện Nacoms", Excerpt = "Một lần tình cờ founder lang thang ở Đồng Tháp…", BodyHtml = "<p>Một lần tình cờ founder lang thang ở Đồng Tháp, ngạc nhiên trước cánh đồng xoài trĩu quả và sự sum suê của trái cây miền Tây. Từ đó, Nacoms ra đời với mong muốn mang nông sản Việt đến gần hơn với người tiêu dùng.</p>", Category = "Câu chuyện", ImageUrl = Img("photo-1622206157934-1f4720b5d0b0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Câu chuyện Nacoms", SeoDescription = "Hành trình mang nông sản miền Tây đến người tiêu dùng.", SeoKeywords = "nacoms, founder, trái cây sấy", PublishedAt = now.AddDays(-90), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0002"), TenantKey = tenant, Slug = "gioi-thieu-chom-chom-say-deo", Title = "Giới thiệu chôm chôm sấy dẻo — món lạ từ Nacoms", Excerpt = "Nhiều khách hàng ngạc nhiên, tò mò và cuối cùng là thích thú với chôm chôm sấy dẻo.", BodyHtml = "<p>Chôm chôm sấy dẻo giữ trọn vị ngọt tự nhiên, dai mềm — món lạ từ vườn cây miền Tây.</p>", Category = "Sản phẩm", ImageUrl = Img("photo-1595475203575-5d2716c8c6c3"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Chôm chôm sấy dẻo Nacoms", SeoDescription = "Giới thiệu sản phẩm chôm chôm sấy dẻo.", SeoKeywords = "chôm chôm, sấy dẻo", PublishedAt = now.AddDays(-10), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0003"), TenantKey = tenant, Slug = "quy-trinh-say-lanh-nacoms", Title = "Quy trình sấy lạnh Nacoms", Excerpt = "Sấy lạnh giữ nguyên màu sắc, hương vị và dưỡng chất của trái cây tươi.", BodyHtml = "<p>Quy trình sấy lạnh kiểm soát nhiệt độ thấp, loại bỏ nước mà không làm mất enzyme và vitamin.</p>", Category = "Tin mới", ImageUrl = Img("photo-1464226184884-fa280b87eda0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Quy trình sấy lạnh", SeoDescription = "Công nghệ sấy lạnh bảo toàn dinh dưỡng.", SeoKeywords = "sấy lạnh, nacoms", PublishedAt = now.AddDays(-30), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0004"), TenantKey = tenant, Slug = "hop-tac-dai-ly-mien-bac", Title = "Hợp tác đại lý miền Bắc", Excerpt = "Nacoms mở rộng kênh phân phối đại lý và private label.", BodyHtml = "<p>Chúng tôi tìm kiếm đối tác phân phối, OEM và private label tại miền Bắc.</p>", Category = "Hợp tác", ImageUrl = Img("photo-1622206157934-1f4720b5d0b0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Hợp tác đại lý", SeoDescription = "Cơ hội hợp tác phân phối và OEM.", SeoKeywords = "đại lý, hợp tác", PublishedAt = now.AddDays(-5), UpdatedAt = now });

        db.SaveChanges();
    }

    public HomeContentDto GetHome(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var banners = db.Banners.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .ToList()
            .Select(ToBannerDto)
            .ToArray();
        var stories = db.StoryBlocks.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .ToList()
            .Select(ToStoryDto)
            .ToArray();
        var articles = db.Articles.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Status == ContentArticleStatuses.Published)
            .OrderByDescending(x => x.PublishedAt)
            .Take(3)
            .ToList()
            .Select(ToArticleDto)
            .ToArray();
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
        var entity = db.Articles.AsNoTracking()
            .FirstOrDefault(x => x.TenantKey == tenantKey && x.Slug == slug);
        return entity is null ? null : ToArticleDto(entity);
    }

    public ArticleDto? GetArticle(Guid id, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Articles.AsNoTracking().FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        return entity is null ? null : ToArticleDto(entity);
    }

    public ArticleDto UpsertArticle(Guid? id, string tenantKey, UpsertArticleRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var entity = id is null
            ? new ContentArticleEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, PublishedAt = request.PublishedAt ?? now, UpdatedAt = now }
            : db.Articles.First(x => x.Id == id && x.TenantKey == tenantKey);

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

        if (id is null) db.Articles.Add(entity);
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

    public PartnershipInquiryDto CreateInquiry(string tenantKey, CreatePartnershipInquiryRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = new ContentPartnershipInquiryEntity
        {
            Id = Guid.NewGuid(),
            TenantKey = tenantKey,
            CompanyName = request.CompanyName.Trim(),
            ContactName = request.ContactName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            PartnershipType = request.PartnershipType.Trim(),
            Message = request.Message.Trim(),
            Status = ContentInquiryStatuses.New,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.PartnershipInquiries.Add(entity);
        db.SaveChanges();
        return ToInquiryDto(entity);
    }

    public IReadOnlyList<PartnershipInquiryDto> ListInquiries(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        return db.PartnershipInquiries.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey)
            .OrderByDescending(x => x.CreatedAt)
            .ToList()
            .Select(ToInquiryDto)
            .ToArray();
    }

    public PartnershipInquiryDto? UpdateInquiryStatus(Guid id, string tenantKey, string status)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.PartnershipInquiries.FirstOrDefault(x => x.Id == id && x.TenantKey == tenantKey);
        if (entity is null) return null;
        var normalized = status.Trim().ToLowerInvariant();
        if (!ContentInquiryStatuses.IsValid(normalized)) return null;
        entity.Status = normalized;
        db.SaveChanges();
        return ToInquiryDto(entity);
    }

    public MediaAssetDto RegisterMedia(string tenantKey, string fileName, string contentType, string publicUrl, long sizeBytes)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = new ContentMediaAssetEntity
        {
            Id = Guid.NewGuid(),
            TenantKey = tenantKey,
            FileName = fileName,
            ContentType = contentType,
            PublicUrl = publicUrl,
            SizeBytes = sizeBytes,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        db.MediaAssets.Add(entity);
        db.SaveChanges();
        return ToMediaDto(entity);
    }

    public IReadOnlyList<MediaAssetDto> ListMedia(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        return db.MediaAssets.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey)
            .OrderByDescending(x => x.UploadedAt)
            .ToList()
            .Select(ToMediaDto)
            .ToArray();
    }

    public NewsletterSubscriptionDto SubscribeNewsletter(string tenantKey, string email)
    {
        using var db = dbFactory.CreateDbContext();
        var normalized = email.Trim().ToLowerInvariant();
        var existing = db.NewsletterSubscriptions.FirstOrDefault(x => x.TenantKey == tenantKey && x.Email == normalized);
        if (existing is not null)
            return new NewsletterSubscriptionDto(existing.Id, existing.TenantKey, existing.Email, existing.SubscribedAt);

        var entity = new ContentNewsletterSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            TenantKey = tenantKey,
            Email = normalized,
            SubscribedAt = DateTimeOffset.UtcNow,
        };
        db.NewsletterSubscriptions.Add(entity);
        db.SaveChanges();
        return new NewsletterSubscriptionDto(entity.Id, entity.TenantKey, entity.Email, entity.SubscribedAt);
    }

    public string BuildSitemapXml(string tenantKey, string baseUrl)
    {
        var articles = ListArticles(tenantKey, publishedOnly: true, locale: null);
        var urls = new List<string>
        {
            $"{baseUrl.TrimEnd('/')}/home",
            $"{baseUrl.TrimEnd('/')}/blog",
            $"{baseUrl.TrimEnd('/')}/cooperation",
        };
        urls.AddRange(articles.Select(article => $"{baseUrl.TrimEnd('/')}/blog/{article.Slug}"));

        var urlset = new XElement(
            "{http://www.sitemaps.org/schemas/sitemap/0.9}urlset",
            urls.Select(url => new XElement(
                "{http://www.sitemaps.org/schemas/sitemap/0.9}url",
                new XElement("{http://www.sitemaps.org/schemas/sitemap/0.9}loc", url))));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlset).ToString();
    }

    public string BuildRssXml(string tenantKey, string baseUrl, string siteTitle)
    {
        var articles = ListArticles(tenantKey, publishedOnly: true, locale: null, take: 20);
        var channel = new XElement(
            "channel",
            new XElement("title", siteTitle),
            new XElement("link", $"{baseUrl.TrimEnd('/')}/blog"),
            new XElement("description", "Latest articles"),
            articles.Select(article => new XElement(
                "item",
                new XElement("title", article.Title),
                new XElement("link", $"{baseUrl.TrimEnd('/')}/blog/{article.Slug}"),
                new XElement("description", article.Excerpt),
                new XElement("pubDate", article.PublishedAt.ToString("R")))));

        var rss = new XElement("rss", new XAttribute("version", "2.0"), channel);
        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), rss).ToString();
    }

    private static string Img(string id) =>
        $"https://images.unsplash.com/{id}?auto=format&fit=crop&w=1200&q=80";

    private static string MapStoryPrefix(string key) => key switch
    {
        "xoai" => "mango",
        "thom" => "pineapple",
        "chanh-day" => "passion",
        "mix" => "mix",
        "tac" => "kumquat",
        "chom" => "rambutan",
        _ => key,
    };

    private static BannerDto ToBannerDto(ContentBannerEntity x) =>
        new(x.Id, x.TenantKey, x.SlideKey, x.ImageUrl, x.EyebrowKey, x.TitleKey, x.SubtitleKey, x.SortOrder, x.IsPublished);

    private static StoryBlockDto ToStoryDto(ContentStoryBlockEntity x) =>
        new(x.Id, x.TenantKey, x.BlockKey, x.TitleKey, x.BodyKey, x.TagKey, x.ImageUrl, x.SortOrder, x.IsPublished);

    private static ArticleDto ToArticleDto(ContentArticleEntity x) =>
        new(x.Id, x.TenantKey, x.Slug, x.Title, x.Excerpt, x.BodyHtml, x.Category, x.ImageUrl, x.Locale, x.Status, x.SeoTitle, x.SeoDescription, x.SeoKeywords, x.PublishedAt, x.UpdatedAt);

    private static PartnershipInquiryDto ToInquiryDto(ContentPartnershipInquiryEntity x) =>
        new(x.Id, x.TenantKey, x.CompanyName, x.ContactName, x.Email, x.Phone, x.PartnershipType, x.Message, x.Status, x.CreatedAt);

    private static MediaAssetDto ToMediaDto(ContentMediaAssetEntity x) =>
        new(x.Id, x.TenantKey, x.FileName, x.ContentType, x.PublicUrl, x.SizeBytes, x.UploadedAt);
}

public static class ContentInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddContentInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<ContentDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ContentDbContext).Assembly.GetName().Name)));
        services.AddSingleton<PostgresContentStore>();
        return services;
    }

    public static void MigrateContentDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ContentDbContext>>();
        using var db = dbFactory.CreateDbContext();
        db.Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PostgresContentStore>().Initialize();
    }
}
