using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class CriticalAlertTriggerType : Enumeration<CriticalAlertTriggerType>
{
    public static readonly CriticalAlertTriggerType CriticalFlag = new("CRITICAL_FLAG", "Critical Flag");
    public static readonly CriticalAlertTriggerType Threshold = new("THRESHOLD", "Threshold");
    public static readonly CriticalAlertTriggerType Both = new("BOTH", "Both");

    private CriticalAlertTriggerType(string code, string name) : base(code, name) { }
}
