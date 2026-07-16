using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class PaymentMethod : Enumeration<PaymentMethod>
{
    public static readonly PaymentMethod Cash = new("CASH", "Cash");
    public static readonly PaymentMethod CreditCard = new("CREDIT_CARD", "Credit Card");
    public static readonly PaymentMethod DebitCard = new("DEBIT_CARD", "Debit Card");
    public static readonly PaymentMethod Insurance = new("INSURANCE", "Insurance");
    public static readonly PaymentMethod BankTransfer = new("BANK_TRANSFER", "Bank Transfer");
    public static readonly PaymentMethod Cheque = new("CHEQUE", "Cheque");

    private PaymentMethod(string code, string name) : base(code, name) { }
}
