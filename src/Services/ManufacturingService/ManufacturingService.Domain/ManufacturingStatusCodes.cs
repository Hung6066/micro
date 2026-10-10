namespace His.Hope.ManufacturingService.Domain;

/// <summary>
/// Persisted manufacturing workflow values. These are owned by the
/// manufacturing bounded context and must not be promoted to shared protocol.
/// </summary>
public static class ManufacturingStatusCodes
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
    public const string Closed = "Closed";
    public const string Started = "Started";
    public const string Created = "Created";
    public const string Released = "Released";
    public const string PartiallyReceived = "PartiallyReceived";
    public const string Pending = "Pending";
    public const string PendingApproval = "PendingApproval";
    public const string Suspended = "Suspended";
}
