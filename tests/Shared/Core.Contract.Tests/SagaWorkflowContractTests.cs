using FluentAssertions;
using His.Hope.Contracts.Saga;
using Xunit;

namespace His.Hope.Core.Contract.Tests;

public sealed class SagaWorkflowContractTests
{
    [Fact]
    public void Catalog_contains_the_five_production_workflows_with_ordered_steps()
    {
        SagaWorkflowCatalog.All.Should().HaveCount(5);
        SagaWorkflowCatalog.CommerceFulfillment.Steps.Should().ContainInOrder(
            SagaWorkflowSteps.AuthorizePayment,
            SagaWorkflowSteps.ReserveInventory,
            SagaWorkflowSteps.CapturePayment,
            SagaWorkflowSteps.CreateShipment);
        SagaWorkflowCatalog.TenantProvisioning.Steps.Should().ContainInOrder(
            SagaWorkflowSteps.RegisterTenant,
            SagaWorkflowSteps.ProvisionTenantData,
            SagaWorkflowSteps.SeedTenantAccess);
        SagaWorkflowCatalog.ContentPublishing.Steps.Should().ContainInOrder(
            SagaWorkflowSteps.PublishContent,
            SagaWorkflowSteps.InvalidateContentCache,
            SagaWorkflowSteps.NotifySubscribers);
    }

    [Fact]
    public void Integration_contracts_carry_stable_idempotency_and_trace_fields()
    {
        var request = new PaymentAuthorizationRequestedV1(
            Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion,
            DateTimeOffset.UtcNow, Guid.NewGuid(), "tenant-a", 10m, "USD", "order-1");

        request.IdempotencyKey.Should().Be("order-1");
        request.SchemaVersion.Should().Be(1);
        SagaMessagingContract.PaymentAuthorized.Should().EndWith(".v1");
    }

    [Fact]
    public void Payment_commands_have_distinct_versioned_routes()
    {
        SagaMessagingContract.PaymentCaptureRequested.Should().NotBe(SagaMessagingContract.PaymentRefundRequested);
        SagaMessagingContract.PaymentCaptureRequested.Should().EndWith(".v1");
        SagaMessagingContract.PaymentRefundRequested.Should().EndWith(".v1");

        var capture = new PaymentCaptureRequestedV1(
            Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
            Guid.NewGuid(), "tenant-a", "provider-payment", 10m, "USD", "capture-1");
        capture.IdempotencyKey.Should().Be("capture-1");
        capture.PaymentId.Should().Be("provider-payment");
    }
}
