namespace His.Hope.PatientService.Infrastructure.Projections;

public sealed class ProcessedProjectionEvent
{
    public Guid EventId { get; set; }
    public string ProjectionName { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
