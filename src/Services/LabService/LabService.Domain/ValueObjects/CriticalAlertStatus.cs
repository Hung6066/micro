using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class CriticalAlertStatus : Enumeration<CriticalAlertStatus>
{
    public static readonly CriticalAlertStatus Open = new("OPEN", "Open");
    public static readonly CriticalAlertStatus Acknowledged = new("ACKNOWLEDGED", "Acknowledged");
    public static readonly CriticalAlertStatus Resolved = new("RESOLVED", "Resolved");

    private CriticalAlertStatus(string code, string name) : base(code, name) { }
}
