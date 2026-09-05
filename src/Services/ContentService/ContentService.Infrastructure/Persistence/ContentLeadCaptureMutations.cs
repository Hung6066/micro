using His.Hope.ContentService.Application;
using His.Hope.ContentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ContentService.Infrastructure;

public sealed partial class PostgresContentStore
{
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
}
