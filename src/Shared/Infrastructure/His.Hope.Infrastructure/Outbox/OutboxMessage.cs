using System.ComponentModel.DataAnnotations;

namespace His.Hope.Infrastructure.Outbox;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required, MaxLength(500)]
    public string Type { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? CorrelationId { get; init; }

    [MaxLength(200)]
    public string? CausationId { get; init; }

    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;

    public DateTime? ProcessedOn { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = OutboxStatus.Pending;

    [MaxLength(1000)]
    public string? Error { get; set; }

    public int RetryCount { get; set; }

    public DateTime? LastRetryOn { get; set; }

    public DateTime? LockExpiresAt { get; set; }

    [MaxLength(100)]
    public string? ClaimedBy { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? DeadLetteredOn { get; set; }
}

public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string DeadLetter = "DeadLetter";
    public const string Skipped = "Skipped";
}
