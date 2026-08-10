using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class LabOrderPriority : Enumeration<LabOrderPriority>
{
    public static readonly LabOrderPriority Routine = new("ROUTINE", "Routine");
    public static readonly LabOrderPriority Urgent = new("URGENT", "Urgent");
    public static readonly LabOrderPriority STAT = new("STAT", "STAT");
    public static readonly LabOrderPriority ASAP = new("ASAP", "ASAP");

    private LabOrderPriority(string code, string name) : base(code, name) { }
}
