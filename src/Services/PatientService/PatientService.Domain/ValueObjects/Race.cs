using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.ValueObjects;

public class Race : Enumeration<Race>
{
    public static readonly Race Asian = new("ASIAN", "Asian");
    public static readonly Race Black = new("BLACK", "Black or African American");
    public static readonly Race Hispanic = new("HISP", "Hispanic or Latino");
    public static readonly Race White = new("WHITE", "White");
    public static readonly Race Other = new("OTHER", "Other");
    public static readonly Race Unknown = new("UNK", "Unknown");

    private Race(string code, string name) : base(code, name) { }
}
