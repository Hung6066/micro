using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.ValueObjects;

public class AbnormalFlag : Enumeration<AbnormalFlag>
{
    public static readonly AbnormalFlag Normal = new("NORMAL", "Normal");
    public static readonly AbnormalFlag Abnormal = new("ABNORMAL", "Abnormal");
    public static readonly AbnormalFlag CriticalHigh = new("CRITICAL_HIGH", "Critical High");
    public static readonly AbnormalFlag CriticalLow = new("CRITICAL_LOW", "Critical Low");

    private AbnormalFlag(string code, string name) : base(code, name) { }
}
