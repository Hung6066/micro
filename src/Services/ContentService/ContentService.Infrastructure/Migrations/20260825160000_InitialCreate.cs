using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.ContentService.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Excerpt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeoDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SeoKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_articles", x => x.Id));

            migrationBuilder.CreateTable(
                name: "content_banners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SlideKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EyebrowKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubtitleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_banners", x => x.Id));

            migrationBuilder.CreateTable(
                name: "content_media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_media_assets", x => x.Id));

            migrationBuilder.CreateTable(
                name: "content_newsletter_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    SubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_newsletter_subscriptions", x => x.Id));

            migrationBuilder.CreateTable(
                name: "content_partnership_inquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartnershipType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_partnership_inquiries", x => x.Id));

            migrationBuilder.CreateTable(
                name: "content_story_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BlockKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TagKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_content_story_blocks", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_content_articles_TenantKey_Slug",
                table: "content_articles",
                columns: new[] { "TenantKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_articles_TenantKey_Status_PublishedAt",
                table: "content_articles",
                columns: new[] { "TenantKey", "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_banners_TenantKey_SortOrder",
                table: "content_banners",
                columns: new[] { "TenantKey", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_content_media_assets_TenantKey_UploadedAt",
                table: "content_media_assets",
                columns: new[] { "TenantKey", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_newsletter_subscriptions_TenantKey_Email",
                table: "content_newsletter_subscriptions",
                columns: new[] { "TenantKey", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_partnership_inquiries_TenantKey_CreatedAt",
                table: "content_partnership_inquiries",
                columns: new[] { "TenantKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_story_blocks_TenantKey_SortOrder",
                table: "content_story_blocks",
                columns: new[] { "TenantKey", "SortOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "content_articles");
            migrationBuilder.DropTable(name: "content_banners");
            migrationBuilder.DropTable(name: "content_media_assets");
            migrationBuilder.DropTable(name: "content_newsletter_subscriptions");
            migrationBuilder.DropTable(name: "content_partnership_inquiries");
            migrationBuilder.DropTable(name: "content_story_blocks");
        }
    }
}
