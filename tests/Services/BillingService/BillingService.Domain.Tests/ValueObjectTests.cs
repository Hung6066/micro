using FluentAssertions;
using His.Hope.BillingService.Domain.ValueObjects;
using Xunit;

namespace His.Hope.BillingService.Domain.Tests;

public class ValueObjectTests
{
    public class InvoiceIdTests
    {
        [Fact]
        public void SameValue_AreEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = InvoiceId.From(guid);
            var id2 = InvoiceId.From(guid);

            id1.Should().Be(id2);
            (id1 == id2).Should().BeTrue();
        }

        [Fact]
        public void DifferentValues_AreNotEqual()
        {
            var id1 = InvoiceId.From(Guid.NewGuid());
            var id2 = InvoiceId.From(Guid.NewGuid());

            id1.Should().NotBe(id2);
            (id1 != id2).Should().BeTrue();
        }

        [Fact]
        public void EmptyGuid_Throws()
        {
            var act = () => new InvoiceId(Guid.Empty);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("value");
        }

        [Fact]
        public void New_ReturnsNonEmptyId()
        {
            var id = InvoiceId.New();

            id.Value.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public void ToString_ReturnsValueAsString()
        {
            var guid = Guid.NewGuid();
            var id = InvoiceId.From(guid);

            id.ToString().Should().Be(guid.ToString());
        }
    }

    public class InvoiceLineItemIdTests
    {
        [Fact]
        public void SameValue_AreEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = InvoiceLineItemId.From(guid);
            var id2 = InvoiceLineItemId.From(guid);

            id1.Should().Be(id2);
        }

        [Fact]
        public void DifferentValues_AreNotEqual()
        {
            var id1 = InvoiceLineItemId.From(Guid.NewGuid());
            var id2 = InvoiceLineItemId.From(Guid.NewGuid());

            id1.Should().NotBe(id2);
        }

        [Fact]
        public void EmptyGuid_Throws()
        {
            var act = () => new InvoiceLineItemId(Guid.Empty);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("value");
        }
    }

    public class PaymentIdTests
    {
        [Fact]
        public void SameValue_AreEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = PaymentId.From(guid);
            var id2 = PaymentId.From(guid);

            id1.Should().Be(id2);
        }

        [Fact]
        public void DifferentValues_AreNotEqual()
        {
            var id1 = PaymentId.From(Guid.NewGuid());
            var id2 = PaymentId.From(Guid.NewGuid());

            id1.Should().NotBe(id2);
        }

        [Fact]
        public void EmptyGuid_Throws()
        {
            var act = () => new PaymentId(Guid.Empty);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("value");
        }
    }

    public class InvoiceStatusTests
    {
        [Fact]
        public void GetAll_ListsAllStatuses()
        {
            var statuses = InvoiceStatus.GetAll();

            statuses.Should().HaveCount(7);
            statuses.Should().Contain(InvoiceStatus.Draft);
            statuses.Should().Contain(InvoiceStatus.Submitted);
            statuses.Should().Contain(InvoiceStatus.PartiallyPaid);
            statuses.Should().Contain(InvoiceStatus.Paid);
            statuses.Should().Contain(InvoiceStatus.Cancelled);
            statuses.Should().Contain(InvoiceStatus.Overdue);
            statuses.Should().Contain(InvoiceStatus.Voided);
        }

        [Theory]
        [InlineData("DRAFT")]
        [InlineData("SUBMITTED")]
        [InlineData("PARTIALLY_PAID")]
        [InlineData("PAID")]
        [InlineData("CANCELLED")]
        [InlineData("OVERDUE")]
        [InlineData("VOIDED")]
        public void FromCode_ValidCode_ReturnsStatus(string code)
        {
            var status = InvoiceStatus.FromCode(code);

            status.Code.Should().Be(code);
        }

        [Fact]
        public void FromCode_InvalidCode_Throws()
        {
            var act = () => InvoiceStatus.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'INVALID' is not a valid InvoiceStatus");
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Submitted")]
        [InlineData("Paid")]
        public void FromName_ValidName_ReturnsStatus(string name)
        {
            var status = InvoiceStatus.FromName(name);

            status.Name.Should().Be(name);
        }

        [Fact]
        public void FromName_InvalidName_Throws()
        {
            var act = () => InvoiceStatus.FromName("Unknown");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'Unknown' is not a valid InvoiceStatus");
        }

        [Fact]
        public void Draft_Code_IsDRAFT()
        {
            InvoiceStatus.Draft.Code.Should().Be("DRAFT");
        }

        [Fact]
        public void Submitted_Code_IsSUBMITTED()
        {
            InvoiceStatus.Submitted.Code.Should().Be("SUBMITTED");
        }
    }

    public class PaymentMethodTests
    {
        [Fact]
        public void GetAll_ListsAllMethods()
        {
            var methods = PaymentMethod.GetAll();

            methods.Should().HaveCount(6);
            methods.Should().Contain(PaymentMethod.Cash);
            methods.Should().Contain(PaymentMethod.CreditCard);
            methods.Should().Contain(PaymentMethod.DebitCard);
            methods.Should().Contain(PaymentMethod.Insurance);
            methods.Should().Contain(PaymentMethod.BankTransfer);
            methods.Should().Contain(PaymentMethod.Cheque);
        }

        [Theory]
        [InlineData("CASH")]
        [InlineData("CREDIT_CARD")]
        [InlineData("DEBIT_CARD")]
        [InlineData("INSURANCE")]
        [InlineData("BANK_TRANSFER")]
        [InlineData("CHEQUE")]
        public void FromCode_ValidCode_ReturnsMethod(string code)
        {
            var method = PaymentMethod.FromCode(code);

            method.Code.Should().Be(code);
        }

        [Fact]
        public void FromCode_InvalidCode_Throws()
        {
            var act = () => PaymentMethod.FromCode("PAYPAL");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'PAYPAL' is not a valid PaymentMethod");
        }
    }

    public class PaymentStatusTests
    {
        [Fact]
        public void GetAll_ListsAllStatuses()
        {
            var statuses = PaymentStatus.GetAll();

            statuses.Should().HaveCount(4);
            statuses.Should().Contain(PaymentStatus.Pending);
            statuses.Should().Contain(PaymentStatus.Completed);
            statuses.Should().Contain(PaymentStatus.Failed);
            statuses.Should().Contain(PaymentStatus.Refunded);
        }

        [Theory]
        [InlineData("PENDING")]
        [InlineData("COMPLETED")]
        [InlineData("FAILED")]
        [InlineData("REFUNDED")]
        public void FromCode_ValidCode_ReturnsStatus(string code)
        {
            var status = PaymentStatus.FromCode(code);

            status.Code.Should().Be(code);
        }

        [Fact]
        public void FromCode_InvalidCode_Throws()
        {
            var act = () => PaymentStatus.FromCode("UNKNOWN");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'UNKNOWN' is not a valid PaymentStatus");
        }
    }

    public class InvoiceLineItemTypeTests
    {
        [Fact]
        public void GetAll_ListsAllTypes()
        {
            var types = InvoiceLineItemType.GetAll();

            types.Should().HaveCount(6);
            types.Should().Contain(InvoiceLineItemType.Service);
            types.Should().Contain(InvoiceLineItemType.Medication);
            types.Should().Contain(InvoiceLineItemType.Supply);
            types.Should().Contain(InvoiceLineItemType.Procedure);
            types.Should().Contain(InvoiceLineItemType.Lab);
            types.Should().Contain(InvoiceLineItemType.Consultation);
        }

        [Theory]
        [InlineData("SERVICE")]
        [InlineData("MEDICATION")]
        [InlineData("SUPPLY")]
        [InlineData("PROCEDURE")]
        [InlineData("LAB")]
        [InlineData("CONSULTATION")]
        public void FromCode_ValidCode_ReturnsType(string code)
        {
            var type = InvoiceLineItemType.FromCode(code);

            type.Code.Should().Be(code);
        }

        [Fact]
        public void FromCode_InvalidCode_Throws()
        {
            var act = () => InvoiceLineItemType.FromCode("OTHER");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'OTHER' is not a valid InvoiceLineItemType");
        }
    }
}
