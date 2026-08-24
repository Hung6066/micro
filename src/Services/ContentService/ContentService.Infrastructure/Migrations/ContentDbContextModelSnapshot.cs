using System;
using His.Hope.ContentService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.ContentService.Infrastructure.Migrations
{
    [DbContext(typeof(ContentDbContext))]
    partial class ContentDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.10");

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentArticleEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("BodyHtml").IsRequired().HasColumnType("text");
                b.Property<string>("Category").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Excerpt").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<string>("ImageUrl").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<string>("Locale").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
                b.Property<DateTimeOffset>("PublishedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("SeoDescription").HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<string>("SeoKeywords").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("SeoTitle").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("Slug").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Status").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Title").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "Slug").IsUnique();
                b.HasIndex("TenantKey", "Status", "PublishedAt");
                b.ToTable("content_articles");
            });

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentBannerEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("EyebrowKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("ImageUrl").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<bool>("IsPublished").HasColumnType("boolean");
                b.Property<string>("SlideKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<int>("SortOrder").HasColumnType("integer");
                b.Property<string>("SubtitleKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("TitleKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "SortOrder");
                b.ToTable("content_banners");
            });

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentMediaAssetEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("ContentType").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("FileName").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("PublicUrl").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<long>("SizeBytes").HasColumnType("bigint");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateTimeOffset>("UploadedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "UploadedAt");
                b.ToTable("content_media_assets");
            });

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentNewsletterSubscriptionEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("character varying(320)");
                b.Property<DateTimeOffset>("SubscribedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "Email").IsUnique();
                b.ToTable("content_newsletter_subscriptions");
            });

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentPartnershipInquiryEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("CompanyName").IsRequired().HasMaxLength(300).HasColumnType("character varying(300)");
                b.Property<string>("ContactName").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("character varying(320)");
                b.Property<string>("Message").IsRequired().HasMaxLength(4000).HasColumnType("character varying(4000)");
                b.Property<string>("PartnershipType").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Phone").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<string>("Status").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "CreatedAt");
                b.ToTable("content_partnership_inquiries");
            });

            modelBuilder.Entity("His.Hope.ContentService.Infrastructure.ContentStoryBlockEntity", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("BlockKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("BodyKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("ImageUrl").IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
                b.Property<bool>("IsPublished").HasColumnType("boolean");
                b.Property<int>("SortOrder").HasColumnType("integer");
                b.Property<string>("TagKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("TenantKey").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("TitleKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.HasKey("Id");
                b.HasIndex("TenantKey", "SortOrder");
                b.ToTable("content_story_blocks");
            });
#pragma warning restore 612, 618
        }
    }
}
