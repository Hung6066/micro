using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.ValueObjects;

public class BloodType : Enumeration<BloodType>
{
    public static readonly BloodType APositive = new("A+", "A Positive");
    public static readonly BloodType ANegative = new("A-", "A Negative");
    public static readonly BloodType BPositive = new("B+", "B Positive");
    public static readonly BloodType BNegative = new("B-", "B Negative");
    public static readonly BloodType ABPositive = new("AB+", "AB Positive");
    public static readonly BloodType ABNegative = new("AB-", "AB Negative");
    public static readonly BloodType OPositive = new("O+", "O Positive");
    public static readonly BloodType ONegative = new("O-", "O Negative");
    public static readonly BloodType Unknown = new("U", "Unknown");

    private BloodType(string code, string name) : base(code, name) { }
}
