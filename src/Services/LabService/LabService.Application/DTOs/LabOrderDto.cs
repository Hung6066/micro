namespace His.Hope.LabService.Application.DTOs;

public class LabOrderDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? EncounterId { get; set; }
    public DateTime OrderDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string PriorityName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<LabTestDto> RequestedTests { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
