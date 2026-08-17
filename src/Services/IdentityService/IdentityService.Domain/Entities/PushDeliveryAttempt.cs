namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Provider delivery audit without retaining notification payloads.</summary>
public sealed class PushDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OutboxId { get; set; }
    public Guid DeviceId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
