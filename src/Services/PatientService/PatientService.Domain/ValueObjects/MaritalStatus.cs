using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.ValueObjects;

public class MaritalStatus : Enumeration<MaritalStatus>
{
    public static readonly MaritalStatus Single = new("S", "Single");
    public static readonly MaritalStatus Married = new("M", "Married");
    public static readonly MaritalStatus Divorced = new("D", "Divorced");
    public static readonly MaritalStatus Widowed = new("W", "Widowed");
    public static readonly MaritalStatus Unknown = new("U", "Unknown");

    private MaritalStatus(string code, string name) : base(code, name) { }
}
