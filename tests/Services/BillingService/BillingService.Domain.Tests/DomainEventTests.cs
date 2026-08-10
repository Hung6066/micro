using FluentAssertions;
using His.Hope.BillingService.Domain.Events;
using His.Hope.SharedKernel.Domain.Common;
using Xunit;

namespace His.Hope.BillingService.Domain.Tests;

public class DomainEventTests
{
    [Fact]
    public void InvoiceCreatedDomainEvent_SetsProperties()
    {
        var invoiceId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        const string invoiceNumber = "INV-001";
        const decimal totalAmount = 150.00m;

        var domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId, patientId, invoiceNumber, totalAmount);

        domainEvent.InvoiceId.Should().Be(invoiceId);
        domainEvent.PatientId.Should().Be(patientId);
        domainEvent.InvoiceNumber.Should().Be(invoiceNumber);
        domainEvent.TotalAmount.Should().Be(totalAmount);
    }

    [Fact]
    public void InvoiceSubmittedDomainEvent_SetsProperties()
    {
        var invoiceId = Guid.NewGuid();

        var domainEvent = new InvoiceSubmittedDomainEvent(invoiceId);

        domainEvent.InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void InvoicePaidDomainEvent_SetsProperties()
    {
        var invoiceId = Guid.NewGuid();
        const decimal amountPaid = 150.00m;
        const decimal totalAmount = 150.00m;

        var domainEvent = new InvoicePaidDomainEvent(invoiceId, amountPaid, totalAmount);

        domainEvent.InvoiceId.Should().Be(invoiceId);
        domainEvent.AmountPaid.Should().Be(amountPaid);
        domainEvent.TotalAmount.Should().Be(totalAmount);
    }

    [Fact]
    public void InvoiceCancelledDomainEvent_SetsProperties()
    {
        var invoiceId = Guid.NewGuid();
        const string reason = "Patient cancelled appointment";

        var domainEvent = new InvoiceCancelledDomainEvent(invoiceId, reason);

        domainEvent.InvoiceId.Should().Be(invoiceId);
        domainEvent.Reason.Should().Be(reason);
    }

    [Fact]
    public void InvoiceVoidedDomainEvent_SetsProperties()
    {
        var invoiceId = Guid.NewGuid();
        const string reason = "Administrative error";

        var domainEvent = new InvoiceVoidedDomainEvent(invoiceId, reason);

        domainEvent.InvoiceId.Should().Be(invoiceId);
        domainEvent.Reason.Should().Be(reason);
    }

    [Fact]
    public void PaymentRecordedDomainEvent_SetsProperties()
    {
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        const decimal amount = 100.00m;

        var domainEvent = new PaymentRecordedDomainEvent(paymentId, invoiceId, amount);

        domainEvent.PaymentId.Should().Be(paymentId);
        domainEvent.InvoiceId.Should().Be(invoiceId);
        domainEvent.Amount.Should().Be(amount);
    }

    [Fact]
    public void DomainEvent_OccurredOn_IsSet()
    {
        var domainEvent = new InvoiceCreatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), "INV-001", 100m);

        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AllDomainEvents_HaveOccurredOnSet()
    {
        var events = new List<IDomainEvent>
        {
            new InvoiceCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "INV-001", 100m),
            new InvoiceSubmittedDomainEvent(Guid.NewGuid()),
            new InvoicePaidDomainEvent(Guid.NewGuid(), 100m, 100m),
            new InvoiceCancelledDomainEvent(Guid.NewGuid(), "reason"),
            new InvoiceVoidedDomainEvent(Guid.NewGuid(), "reason"),
            new PaymentRecordedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), 100m)
        };

        foreach (var domainEvent in events)
        {
            domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}
