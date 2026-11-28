using His.Hope.ContentService.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.ContentService.Infrastructure.Migrations;

/// <summary>
/// Adds the shared data-lifecycle audit columns that HisHopeDataConventions maps
/// on every content table but no applied migration ever created. Idempotent.
/// </summary>
[DbContext(typeof(ContentDbContext))]
[Migration("20261122100000_RepairContentDataLifecycle")]
public partial class RepairContentDataLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_articles ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_banners ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_media_assets ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_newsletter_subscriptions ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_partnership_inquiries ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS created_by character varying(256);
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS updated_by character varying(256);
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false;
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
        ALTER TABLE content_story_blocks ADD COLUMN IF NOT EXISTS deleted_by character varying(256);
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
