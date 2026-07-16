using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class LabTestStatus : Enumeration<LabTestStatus>
{
    public static readonly LabTestStatus Ordered = new("ORDERED", "Ordered");
    public static readonly LabTestStatus Collected = new("COLLECTED", "Collected");
    public static readonly LabTestStatus InProgress = new("IN_PROGRESS", "In Progress");
    public static readonly LabTestStatus Resulted = new("RESULTED", "Resulted");
    public static readonly LabTestStatus Cancelled = new("CANCELLED", "Cancelled");

    private LabTestStatus(string code, string name) : base(code, name) { }
}
