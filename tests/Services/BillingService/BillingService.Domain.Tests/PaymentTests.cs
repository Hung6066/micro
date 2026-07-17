using FluentAssertions;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;
using Xunit;

namespace His.Hope.BillingService.Domain.Tests;

public class PaymentTests
{
    private static readonly InvoiceId DefaultInvoiceId = InvoiceId.New();
    private static readonly Guid DefaultPatientId = Guid.NewGuid();
    private const decimal DefaultAmount = 150.00m;
    private static readonly DateTime DefaultPaymentDate = new(2024, 3, 15);
    private static readonly PaymentMethod DefaultMethod = PaymentMethod.CreditCard;
    private const string DefaultReferenceNumber = "REF-001";
    private const string DefaultNotes = "Card payment";

    private Payment CreateDefaultPayment()
    {
        return Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            DefaultAmount,
            DefaultPaymentDate,
            DefaultMethod,
            DefaultReferenceNumber,
            DefaultNotes);
    }

    [Fact]
    public void Create_WithValidParameters_CreatesPayment()
    {
        var payment = Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            DefaultAmount,
            DefaultPaymentDate,
            DefaultMethod,
            DefaultReferenceNumber,
            DefaultNotes);

        payment.Should().NotBeNull();
        payment.InvoiceId.Should().Be(DefaultInvoiceId);
        payment.PatientId.Should().Be(DefaultPatientId);
        payment.Amount.Should().Be(DefaultAmount);
        payment.PaymentDate.Should().Be(DefaultPaymentDate);
        payment.Method.Should().Be(DefaultMethod);
        payment.ReferenceNumber.Should().Be(DefaultReferenceNumber);
        payment.Notes.Should().Be(DefaultNotes);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithZeroAmount_Throws()
    {
        var act = () => Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            0,
            DefaultPaymentDate,
            DefaultMethod,
            DefaultReferenceNumber,
            DefaultNotes);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("amount");
    }

    [Fact]
    public void Create_WithNegativeAmount_Throws()
    {
        var act = () => Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            -50.00m,
            DefaultPaymentDate,
            DefaultMethod,
            DefaultReferenceNumber,
            DefaultNotes);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("amount");
    }

    [Fact]
    public void Create_WithNullMethod_Throws()
    {
        var act = () => Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            DefaultAmount,
            DefaultPaymentDate,
            null!,
            DefaultReferenceNumber,
            DefaultNotes);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("method");
    }

    [Fact]
    public void Create_WithoutReferenceAndNotes_CreatesPayment()
    {
        var payment = Payment.Create(
            DefaultInvoiceId,
            DefaultPatientId,
            DefaultAmount,
            DefaultPaymentDate,
            DefaultMethod,
            null,
            null);

        payment.ReferenceNumber.Should().BeNull();
        payment.Notes.Should().BeNull();
    }

    [Fact]
    public void MarkCompleted_TransitionsToCompleted()
    {
        var payment = CreateDefaultPayment();

        payment.MarkCompleted();

        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public void MarkFailed_TransitionsToFailed()
    {
        var payment = CreateDefaultPayment();

        payment.MarkFailed();

        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Refund_WhenCompleted_TransitionsToRefunded()
    {
        var payment = CreateDefaultPayment();
        payment.MarkCompleted();

        payment.Refund();

        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refund_WhenPending_Throws()
    {
        var payment = CreateDefaultPayment();

        var act = () => payment.Refund();

        act.Should().Throw<DomainException>()
            .WithMessage("Only completed payments can be refunded.");
    }

    [Fact]
    public void Refund_WhenAlreadyRefunded_Throws()
    {
        var payment = CreateDefaultPayment();
        payment.MarkCompleted();
        payment.Refund();

        var act = () => payment.Refund();

        act.Should().Throw<DomainException>()
            .WithMessage("Payment has already been refunded.");
    }

    [Fact]
    public void MarkCompleted_ThenMarkFailed_DoesNotChangeStatus()
    {
        var payment = CreateDefaultPayment();
        payment.MarkCompleted();
        payment.MarkFailed();

        payment.Status.Should().Be(PaymentStatus.Failed);
    }
}
