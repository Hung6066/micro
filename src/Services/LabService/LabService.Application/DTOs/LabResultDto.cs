namespace His.Hope.LabService.Application.DTOs;

public class LabResultDto
{
    public Guid LabResultId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AbnormalFlagCode { get; set; }
    public string? AbnormalFlagName { get; set; }
    public string ResultStatusCode { get; set; } = string.Empty;
    public string ResultStatusName { get; set; } = string.Empty;
    public DateTime ResultedAt { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
}
