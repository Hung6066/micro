using FluentAssertions;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Events;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;
using Xunit;

namespace His.Hope.BillingService.Domain.Tests;

public class InvoiceTests
{
    private static readonly Guid DefaultPatientId = Guid.NewGuid();
    private static readonly Guid DefaultEncounterId = Guid.NewGuid();
    private const string DefaultInvoiceNumber = "INV-2024-001";
    private static readonly DateTime DefaultInvoiceDate = new(2024, 3, 15);
    private static readonly DateTime DefaultDueDate = new(2024, 4, 15);
    private const string DefaultNotes = "Regular consultation";

    private Invoice CreateDefaultInvoice()
    {
        return Invoice.Create(
            DefaultPatientId,
            DefaultEncounterId,
            DefaultInvoiceNumber,
            DefaultInvoiceDate,
            DefaultDueDate,
            DefaultNotes);
    }

    private InvoiceLineItem CreateDefaultLineItem(InvoiceId invoiceId)
    {
        return InvoiceLineItem.Create(
            invoiceId,
            "Consultation fee",
            1,
            150.00m,
            "CONS001",
            InvoiceLineItemType.Service);
    }

    [Fact]
    public void Create_WithValidParameters_CreatesDraftInvoice()
    {
        var invoice = Invoice.Create(
            DefaultPatientId,
            DefaultEncounterId,
            DefaultInvoiceNumber,
            DefaultInvoiceDate,
            DefaultDueDate,
            DefaultNotes);

        invoice.Should().NotBeNull();
        invoice.PatientId.Should().Be(DefaultPatientId);
        invoice.EncounterId.Should().Be(DefaultEncounterId);
        invoice.InvoiceNumber.Should().Be(DefaultInvoiceNumber);
        invoice.InvoiceDate.Should().Be(DefaultInvoiceDate);
        invoice.DueDate.Should().Be(DefaultDueDate);
        invoice.Notes.Should().Be(DefaultNotes);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.SubTotal.Should().Be(0);
        invoice.TaxAmount.Should().Be(0);
        invoice.DiscountAmount.Should().Be(0);
        invoice.PaidAmount.Should().Be(0);
        invoice.TotalAmount.Should().Be(0);
        invoice.BalanceDue.Should().Be(0);
        invoice.LineItems.Should().BeEmpty();
        invoice.Payments.Should().BeEmpty();
        invoice.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyInvoiceNumber_Throws()
    {
        var act = () => Invoice.Create(
            DefaultPatientId,
            DefaultEncounterId,
            "",
            DefaultInvoiceDate,
            DefaultDueDate,
            DefaultNotes);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("invoiceNumber");
    }

    [Fact]
    public void Create_RaisesInvoiceCreatedDomainEvent()
    {
        var invoice = Invoice.Create(
            DefaultPatientId,
            DefaultEncounterId,
            DefaultInvoiceNumber,
            DefaultInvoiceDate,
            DefaultDueDate,
            DefaultNotes);

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCreatedDomainEvent>()
            .Which.InvoiceId.Should().Be(invoice.Id.Value);
    }

    [Fact]
    public void AddLineItem_ToDraftInvoice_AddsItemAndRecalculates()
    {
        var invoice = CreateDefaultInvoice();
        var lineItem = CreateDefaultLineItem(invoice.Id);

        invoice.AddLineItem(lineItem);

        invoice.LineItems.Should().HaveCount(1);
        invoice.LineItems.Should().Contain(lineItem);
        invoice.SubTotal.Should().Be(150.00m);
        invoice.TotalAmount.Should().Be(150.00m);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddLineItem_ToNonDraftInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Submit();

        var lineItem = CreateDefaultLineItem(invoice.Id);
        var act = () => invoice.AddLineItem(lineItem);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot add line items to a non-draft invoice.");
    }

    [Fact]
    public void RemoveLineItem_FromDraftInvoice_RemovesItem()
    {
        var invoice = CreateDefaultInvoice();
        var lineItem = CreateDefaultLineItem(invoice.Id);
        invoice.AddLineItem(lineItem);

        invoice.RemoveLineItem(lineItem.Id);

        invoice.LineItems.Should().BeEmpty();
        invoice.SubTotal.Should().Be(0);
        invoice.TotalAmount.Should().Be(0);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RemoveLineItem_LineItemNotFound_Throws()
    {
        var invoice = CreateDefaultInvoice();
        var nonExistentId = InvoiceLineItemId.New();

        var act = () => invoice.RemoveLineItem(nonExistentId);

        act.Should().Throw<DomainException>()
            .WithMessage("Line item not found on this invoice.");
    }

    [Fact]
    public void RemoveLineItem_FromNonDraftInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        var lineItem = CreateDefaultLineItem(invoice.Id);
        invoice.AddLineItem(lineItem);
        invoice.Submit();

        var act = () => invoice.RemoveLineItem(lineItem.Id);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot remove line items from a non-draft invoice.");
    }

    [Fact]
    public void ApplyDiscount_ToDraftInvoice_ReducesTotal()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.ApplyDiscount(30.00m);

        invoice.DiscountAmount.Should().Be(30.00m);
        invoice.TotalAmount.Should().Be(120.00m);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApplyDiscount_WithNegativeAmount_Throws()
    {
        var invoice = CreateDefaultInvoice();

        var act = () => invoice.ApplyDiscount(-10.00m);

        act.Should().Throw<DomainException>()
            .WithMessage("Discount amount cannot be negative.");
    }

    [Fact]
    public void ApplyDiscount_ExceedingSubTotal_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        var act = () => invoice.ApplyDiscount(200.00m);

        act.Should().Throw<DomainException>()
            .WithMessage("Discount cannot exceed subtotal.");
    }

    [Fact]
    public void ApplyDiscount_ToNonDraftInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Submit();

        var act = () => invoice.ApplyDiscount(10.00m);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot apply discount to a non-draft invoice.");
    }

    [Fact]
    public void ApplyTax_ToDraftInvoice_IncreasesTotal()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.ApplyTax(25.00m);

        invoice.TaxAmount.Should().Be(25.00m);
        invoice.TotalAmount.Should().Be(175.00m);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApplyTax_WithNegativeAmount_Throws()
    {
        var invoice = CreateDefaultInvoice();

        var act = () => invoice.ApplyTax(-5.00m);

        act.Should().Throw<DomainException>()
            .WithMessage("Tax amount cannot be negative.");
    }

    [Fact]
    public void ApplyTax_ToNonDraftInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Submit();

        var act = () => invoice.ApplyTax(25.00m);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot apply tax to a non-draft invoice.");
    }

    [Fact]
    public void Submit_WithLineItems_TransitionsToSubmitted()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.Submit();

        invoice.Status.Should().Be(InvoiceStatus.Submitted);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Submit_WithNoLineItems_Throws()
    {
        var invoice = CreateDefaultInvoice();

        var act = () => invoice.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot submit an invoice with no line items.");
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Submit();

        var act = () => invoice.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Invoice has already been submitted.");
    }

    [Fact]
    public void Submit_WhenPaid_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();
        invoice.RecordPayment(payment);

        var act = () => invoice.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot submit a paid invoice.");
    }

    [Fact]
    public void Submit_WhenCancelled_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Cancel("No longer needed");

        var act = () => invoice.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot submit a cancelled invoice.");
    }

    [Fact]
    public void Submit_RaisesInvoiceSubmittedDomainEvent()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ClearDomainEvents();

        invoice.Submit();

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceSubmittedDomainEvent>()
            .Which.InvoiceId.Should().Be(invoice.Id.Value);
    }

    [Fact]
    public void MarkPaid_TransitionsToPaid()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.MarkPaid();

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkPaid_WhenAlreadyPaid_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.MarkPaid();

        var act = () => invoice.MarkPaid();

        act.Should().Throw<DomainException>()
            .WithMessage("Invoice is already paid.");
    }

    [Fact]
    public void MarkPaid_WhenCancelled_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Cancel("No longer needed");

        var act = () => invoice.MarkPaid();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot mark a cancelled invoice as paid.");
    }

    [Fact]
    public void MarkPaid_RaisesInvoicePaidDomainEvent()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ClearDomainEvents();

        invoice.MarkPaid();

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoicePaidDomainEvent>()
            .Which.InvoiceId.Should().Be(invoice.Id.Value);
    }

    [Fact]
    public void Cancel_TransitionsToCancelled()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.Cancel("Patient cancelled appointment");

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.Notes.Should().Be("Patient cancelled appointment");
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Cancel("First cancellation");

        var act = () => invoice.Cancel("Second cancellation");

        act.Should().Throw<DomainException>()
            .WithMessage("Invoice has already been cancelled.");
    }

    [Fact]
    public void Cancel_WhenPaid_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();
        invoice.RecordPayment(payment);

        var act = () => invoice.Cancel("Cannot cancel paid");

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot cancel a paid invoice.");
    }

    [Fact]
    public void Cancel_RaisesInvoiceCancelledDomainEvent()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ClearDomainEvents();

        invoice.Cancel("Patient request");

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCancelledDomainEvent>()
            .Which.InvoiceId.Should().Be(invoice.Id.Value);
    }

    [Fact]
    public void Void_TransitionsToVoided()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));

        invoice.Void("Administrative error");

        invoice.Status.Should().Be(InvoiceStatus.Voided);
        invoice.Notes.Should().Be("Administrative error");
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Void_WhenAlreadyVoided_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Void("First void");

        var act = () => invoice.Void("Second void");

        act.Should().Throw<DomainException>()
            .WithMessage("Invoice has already been voided.");
    }

    [Fact]
    public void Void_WhenPaid_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();
        invoice.RecordPayment(payment);

        var act = () => invoice.Void("Cannot void paid");

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot void a paid invoice.");
    }

    [Fact]
    public void Void_RaisesInvoiceVoidedDomainEvent()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ClearDomainEvents();

        invoice.Void("Administrative void");

        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceVoidedDomainEvent>()
            .Which.InvoiceId.Should().Be(invoice.Id.Value);
    }

    [Fact]
    public void RecordPayment_AddsPaymentAndUpdatesBalance()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 100.00m,
            DateTime.UtcNow, PaymentMethod.CreditCard, "REF-001", null);
        payment.MarkCompleted();

        invoice.RecordPayment(payment);

        invoice.Payments.Should().HaveCount(1);
        invoice.Payments.Should().Contain(payment);
        invoice.PaidAmount.Should().Be(100.00m);
        invoice.BalanceDue.Should().Be(50.00m);
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordPayment_OnCancelledInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.Cancel("Cancelled");
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 100.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);

        var act = () => invoice.RecordPayment(payment);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot record payment on a cancelled invoice.");
    }

    [Fact]
    public void RecordPayment_OnPaidInvoice_Throws()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment1 = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment1.MarkCompleted();
        invoice.RecordPayment(payment1);
        var payment2 = Payment.Create(
            invoice.Id, DefaultPatientId, 50.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);

        var act = () => invoice.RecordPayment(payment2);

        act.Should().Throw<DomainException>()
            .WithMessage("Invoice is already fully paid.");
    }

    [Fact]
    public void RecordPayment_WithCompletedPayment_FullyPaid_MarksPaidStatus()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.BankTransfer, "BT-001", null);
        payment.MarkCompleted();

        invoice.RecordPayment(payment);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAmount.Should().Be(150.00m);
        invoice.BalanceDue.Should().Be(0);
    }

    [Fact]
    public void RecordPayment_WithPartialPayment_MarksPartiallyPaid()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 50.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();

        invoice.RecordPayment(payment);

        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        invoice.PaidAmount.Should().Be(50.00m);
        invoice.BalanceDue.Should().Be(100.00m);
    }

    [Fact]
    public void RecordPayment_RaisesPaymentRecordedDomainEvent()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ClearDomainEvents();
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 50.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();

        invoice.RecordPayment(payment);

        invoice.DomainEvents.Should().Contain(e => e is PaymentRecordedDomainEvent);
    }

    [Fact]
    public void TotalAmount_CalculatesCorrectly()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        invoice.ApplyTax(25.00m);
        invoice.ApplyDiscount(10.00m);

        invoice.SubTotal.Should().Be(150.00m);
        invoice.TaxAmount.Should().Be(25.00m);
        invoice.DiscountAmount.Should().Be(10.00m);
        invoice.TotalAmount.Should().Be(165.00m);
    }

    [Fact]
    public void BalanceDue_AfterFullPayment_IsZero()
    {
        var invoice = CreateDefaultInvoice();
        invoice.AddLineItem(CreateDefaultLineItem(invoice.Id));
        var payment = Payment.Create(
            invoice.Id, DefaultPatientId, 150.00m,
            DateTime.UtcNow, PaymentMethod.Cash, null, null);
        payment.MarkCompleted();

        invoice.RecordPayment(payment);

        invoice.BalanceDue.Should().Be(0);
    }
}
