namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Durable push work item. Delivery is retried by the identity worker.</summary>
public sealed class PushNotificationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
