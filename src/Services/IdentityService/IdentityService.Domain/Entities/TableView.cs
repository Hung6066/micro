namespace His.Hope.IdentityService.Domain.Entities;

public sealed class TableView
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
