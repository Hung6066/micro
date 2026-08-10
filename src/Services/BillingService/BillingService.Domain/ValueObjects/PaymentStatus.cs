using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class PaymentStatus : Enumeration<PaymentStatus>
{
    public static readonly PaymentStatus Pending = new("PENDING", "Pending");
    public static readonly PaymentStatus Completed = new("COMPLETED", "Completed");
    public static readonly PaymentStatus Failed = new("FAILED", "Failed");
    public static readonly PaymentStatus Refunded = new("REFUNDED", "Refunded");

    private PaymentStatus(string code, string name) : base(code, name) { }
}
