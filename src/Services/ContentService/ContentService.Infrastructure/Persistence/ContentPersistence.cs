using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using His.Hope.Contracts.Saga;

namespace His.Hope.ContentService.Infrastructure;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<ContentBannerEntity> Banners => Set<ContentBannerEntity>();
    public DbSet<ContentStoryBlockEntity> StoryBlocks => Set<ContentStoryBlockEntity>();
    public DbSet<ContentArticleEntity> Articles => Set<ContentArticleEntity>();
    public DbSet<ContentPartnershipInquiryEntity> PartnershipInquiries => Set<ContentPartnershipInquiryEntity>();
    public DbSet<ContentMediaAssetEntity> MediaAssets => Set<ContentMediaAssetEntity>();
    public DbSet<ContentNewsletterSubscriptionEntity> NewsletterSubscriptions => Set<ContentNewsletterSubscriptionEntity>();
    public DbSet<ContentPublishingOutboxEntity> PublishingOutbox => Set<ContentPublishingOutboxEntity>();

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
        modelBuilder.Entity<ContentPublishingOutboxEntity>(entity =>
        {
            entity.ToTable("content_publishing_outbox"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Type).HasColumnName("type");
            entity.Property(x => x.Content).HasColumnName("content");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.ProcessedOn).HasColumnName("processed_on");
            entity.Property(x => x.LeaseUntil).HasColumnName("lease_until");
            entity.Property(x => x.AttemptCount).HasColumnName("attempt_count");
            entity.Property(x => x.LastAttemptedAt).HasColumnName("last_attempted_at");
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => x.ProcessedOn);
        });

        HisHopeDataConventions.Apply(
            modelBuilder,
            typeof(ContentBannerEntity), typeof(ContentStoryBlockEntity), typeof(ContentArticleEntity),
            typeof(ContentPartnershipInquiryEntity), typeof(ContentMediaAssetEntity),
            typeof(ContentNewsletterSubscriptionEntity));
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

public sealed class ContentPublishingOutboxEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptedAt { get; set; }
    public string? LastError { get; set; }
}

public sealed partial class PostgresContentStore
{
    private readonly IDbContextFactory<ContentDbContext> dbFactory;

    public PostgresContentStore(IDbContextFactory<ContentDbContext> dbFactory) =>
        this.dbFactory = dbFactory;

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
    public static IServiceCollection AddContentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("ContentDb");
        if (string.IsNullOrWhiteSpace(connection))
            return services;

        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddHisHopeTenantAwareDbContextFactory<ContentDbContext>(
            "content",
            (sp, builder, connectionString, connectionName) =>
                builder.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(ContentDbContext).Assembly.GetName().Name))
                    .AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>()));
        services.AddSingleton<IContentDbContextFactory>(sp =>
            new ContentDbContextFactoryBridge(sp.GetRequiredService<IHisHopeDbContextFactory<ContentDbContext>>()));
        services.AddSingleton<PostgresContentStore>();
        if (!string.Equals(configuration["HIS_HOPE_ENVIRONMENT"], "Testing", StringComparison.OrdinalIgnoreCase))
            services.AddHostedService<Messaging.ContentPublishingOutboxDispatcher>();
        return services;
    }

    public static void MigrateContentDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IContentDbContextFactory>();
        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            using var db = dbFactory.CreateDbContextForConnection(connectionName);
            db.Database.Migrate();
        }

        scope.ServiceProvider.GetRequiredService<PostgresContentStore>().Initialize();
    }
}
