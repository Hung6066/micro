using Microsoft.EntityFrameworkCore;
using His.Hope.ContentService.Domain;

namespace His.Hope.ContentService.Infrastructure;

public sealed partial class PostgresContentStore
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
                Id = Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb{i + 1:D4}"), TenantKey = tenant, BlockKey = key,
                TitleKey = $"buyer.home.story.{MapStoryPrefix(key)}.title", BodyKey = $"buyer.home.story.{MapStoryPrefix(key)}.body",
                TagKey = $"buyer.home.story.{MapStoryPrefix(key)}.tag", ImageUrl = storyImages[i], SortOrder = i, IsPublished = true,
            });
        }

        db.Articles.AddRange(
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0001"), TenantKey = tenant, Slug = "founder-story", Title = "Câu chuyện Nacoms", Excerpt = "Một lần tình cờ founder lang thang ở Đồng Tháp…", BodyHtml = "<p>Một lần tình cờ founder lang thang ở Đồng Tháp, ngạc nhiên trước cánh đồng xoài trĩu quả và sự sum suê của trái cây miền Tây. Từ đó, Nacoms ra đời với mong muốn mang nông sản Việt đến gần hơn với người tiêu dùng.</p>", Category = "Câu chuyện", ImageUrl = Img("photo-1622206157934-1f4720b5d0b0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Câu chuyện Nacoms", SeoDescription = "Hành trình mang nông sản miền Tây đến người tiêu dùng.", SeoKeywords = "nacoms, founder, trái cây sấy", PublishedAt = now.AddDays(-90), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0002"), TenantKey = tenant, Slug = "gioi-thieu-chom-chom-say-deo", Title = "Giới thiệu chôm chôm sấy dẻo — món lạ từ Nacoms", Excerpt = "Nhiều khách hàng ngạc nhiên, tò mò và cuối cùng là thích thú với chôm chôm sấy dẻo.", BodyHtml = "<p>Chôm chôm sấy dẻo giữ trọn vị ngọt tự nhiên, dai mềm — món lạ từ vườn cây miền Tây.</p>", Category = "Sản phẩm", ImageUrl = Img("photo-1595475203575-5d2716c8c6c3"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Chôm chôm sấy dẻo Nacoms", SeoDescription = "Giới thiệu sản phẩm chôm chôm sấy dẻo.", SeoKeywords = "chôm chôm, sấy dẻo", PublishedAt = now.AddDays(-10), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0003"), TenantKey = tenant, Slug = "quy-trinh-say-lanh-nacoms", Title = "Quy trình sấy lạnh Nacoms", Excerpt = "Sấy lạnh giữ nguyên màu sắc, hương vị và dưỡng chất của trái cây tươi.", BodyHtml = "<p>Quy trình sấy lạnh kiểm soát nhiệt độ thấp, loại bỏ nước mà không làm mất enzyme và vitamin.</p>", Category = "Tin mới", ImageUrl = Img("photo-1464226184884-fa280b87eda0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Quy trình sấy lạnh", SeoDescription = "Công nghệ sấy lạnh bảo toàn dinh dưỡng.", SeoKeywords = "sấy lạnh, nacoms", PublishedAt = now.AddDays(-30), UpdatedAt = now },
            new ContentArticleEntity { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0004"), TenantKey = tenant, Slug = "hop-tac-dai-ly-mien-bac", Title = "Hợp tác đại lý miền Bắc", Excerpt = "Nacoms mở rộng kênh phân phối đại lý và private label.", BodyHtml = "<p>Chúng tôi tìm kiếm đối tác phân phối, OEM và private label tại miền Bắc.</p>", Category = "Hợp tác", ImageUrl = Img("photo-1622206157934-1f4720b5d0b0"), Locale = "vi-VN", Status = ContentArticleStatuses.Published, SeoTitle = "Hợp tác đại lý", SeoDescription = "Cơ hội hợp tác phân phối và OEM.", SeoKeywords = "đại lý, hợp tác", PublishedAt = now.AddDays(-5), UpdatedAt = now });

        db.SaveChanges();
    }

    private static string Img(string id) =>
        $"https://images.unsplash.com/{id}?auto=format&fit=crop&w=1200&q=80";

    private static string MapStoryPrefix(string key) => key switch
    {
        "xoai" => "mango", "thom" => "pineapple", "chanh-day" => "passion", "mix" => "mix", "tac" => "kumquat", "chom" => "rambutan", _ => key,
    };
}
