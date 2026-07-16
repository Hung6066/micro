namespace His.Hope.LabService.Application.DTOs;

public class LabTestDto
{
    public Guid Id { get; set; }
    public Guid LabOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? SpecimenType { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public LabResultDto? Result { get; set; }
    public DateTime OrderedAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
