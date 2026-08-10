using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class LabOrderStatus : Enumeration<LabOrderStatus>
{
    public static readonly LabOrderStatus Pending = new("PENDING", "Pending");
    public static readonly LabOrderStatus Submitted = new("SUBMITTED", "Submitted");
    public static readonly LabOrderStatus InProgress = new("IN_PROGRESS", "In Progress");
    public static readonly LabOrderStatus Completed = new("COMPLETED", "Completed");
    public static readonly LabOrderStatus Cancelled = new("CANCELLED", "Cancelled");

    private LabOrderStatus(string code, string name) : base(code, name) { }
}
