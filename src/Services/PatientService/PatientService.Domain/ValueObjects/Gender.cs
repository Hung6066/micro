using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.ValueObjects;

public class Gender : Enumeration<Gender>
{
    public static readonly Gender Male = new("M", "Male");
    public static readonly Gender Female = new("F", "Female");
    public static readonly Gender Other = new("O", "Other");
    public static readonly Gender Unknown = new("U", "Unknown");

    private Gender(string code, string name) : base(code, name) { }
}
