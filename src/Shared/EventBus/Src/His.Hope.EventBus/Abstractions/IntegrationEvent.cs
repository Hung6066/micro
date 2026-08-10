namespace His.Hope.EventBus.Abstractions;

public abstract class IntegrationEvent
{
    public Guid Id { get; }
    public DateTime CreationDate { get; }
    public int SchemaVersion { get; set; } = 1;
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public Dictionary<string, string>? Headers { get; set; }

    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
    }
}
