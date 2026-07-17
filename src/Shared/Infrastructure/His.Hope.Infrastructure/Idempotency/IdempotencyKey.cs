namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// Represents an idempotency key record for safe retry of mutating API requests.
/// Maps to the <c>idempotency_keys</c> table.
/// </summary>
public class IdempotencyKey
{
    public string IdempotencyKeyValue { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string Status { get; set; } = "Processing";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
