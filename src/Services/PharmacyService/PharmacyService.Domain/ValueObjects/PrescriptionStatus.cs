using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.ValueObjects;

public class PrescriptionStatus : Enumeration<PrescriptionStatus>
{
    public static readonly PrescriptionStatus Prescribed = new("PRESCRIBED", "Prescribed");
    public static readonly PrescriptionStatus Filled = new("FILLED", "Filled");
    public static readonly PrescriptionStatus Cancelled = new("CANCELLED", "Cancelled");
    public static readonly PrescriptionStatus Expired = new("EXPIRED", "Expired");

    private PrescriptionStatus(string code, string name) : base(code, name) { }
}
