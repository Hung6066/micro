using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class LabResultStatus : Enumeration<LabResultStatus>
{
    public static readonly LabResultStatus Pending = new("PENDING", "Pending");
    public static readonly LabResultStatus Preliminary = new("PRELIMINARY", "Preliminary");
    public static readonly LabResultStatus Final = new("FINAL", "Final");
    public static readonly LabResultStatus Corrected = new("CORRECTED", "Corrected");

    private LabResultStatus(string code, string name) : base(code, name) { }
}
