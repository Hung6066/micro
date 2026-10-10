namespace His.Hope.Contracts.Saga;

/// <summary>
/// Stable names and ordered steps for cross-service workflows. These values are
/// part of the integration contract: changing them requires a new schema/version.
/// </summary>
public static class SagaWorkflowNames
{
    public const string CommerceFulfillment = "commerce.fulfillment";
    public const string Payment = "commerce.payment";
    public const string Shipment = "commerce.shipment";
    public const string TenantProvisioning = "identity.tenant-provisioning";
    public const string ContentPublishing = "content.publishing";
}

public static class SagaWorkflowSteps
{
    public const string ValidateOrder = "validate-order";
    public const string AuthorizePayment = "authorize-payment";
    public const string ReserveInventory = "reserve-inventory";
    public const string CapturePayment = "capture-payment";
    public const string CreateShipment = "create-shipment";
    public const string DispatchShipment = "dispatch-shipment";
    public const string CancelShipment = "cancel-shipment";
    public const string RefundPayment = "refund-payment";
    public const string RegisterTenant = "register-tenant";
    public const string ProvisionTenantData = "provision-tenant-data";
    public const string SeedTenantAccess = "seed-tenant-access";
    public const string PublishContent = "publish-content";
    public const string InvalidateContentCache = "invalidate-content-cache";
    public const string NotifySubscribers = "notify-subscribers";
}

public sealed record SagaWorkflowDefinition(
    string Name,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Compensations);

public static class SagaWorkflowCatalog
{
    public static SagaWorkflowDefinition CommerceFulfillment { get; } = new(
        SagaWorkflowNames.CommerceFulfillment,
        [SagaWorkflowSteps.ValidateOrder, SagaWorkflowSteps.AuthorizePayment,
         SagaWorkflowSteps.ReserveInventory, SagaWorkflowSteps.CapturePayment,
         SagaWorkflowSteps.CreateShipment, SagaWorkflowSteps.DispatchShipment],
        [SagaWorkflowSteps.RefundPayment, SagaWorkflowSteps.CancelShipment,
         SagaWorkflowSteps.ReserveInventory, SagaWorkflowSteps.AuthorizePayment]);

    public static SagaWorkflowDefinition Payment { get; } = new(
        SagaWorkflowNames.Payment,
        [SagaWorkflowSteps.AuthorizePayment, SagaWorkflowSteps.CapturePayment],
        [SagaWorkflowSteps.RefundPayment]);

    public static SagaWorkflowDefinition Shipment { get; } = new(
        SagaWorkflowNames.Shipment,
        [SagaWorkflowSteps.CreateShipment, SagaWorkflowSteps.DispatchShipment], []);

    public static SagaWorkflowDefinition TenantProvisioning { get; } = new(
        SagaWorkflowNames.TenantProvisioning,
        [SagaWorkflowSteps.RegisterTenant, SagaWorkflowSteps.ProvisionTenantData,
         SagaWorkflowSteps.SeedTenantAccess], []);

    public static SagaWorkflowDefinition ContentPublishing { get; } = new(
        SagaWorkflowNames.ContentPublishing,
        [SagaWorkflowSteps.PublishContent, SagaWorkflowSteps.InvalidateContentCache,
         SagaWorkflowSteps.NotifySubscribers], []);

    public static IReadOnlyList<SagaWorkflowDefinition> All { get; } =
        [CommerceFulfillment, Payment, Shipment, TenantProvisioning, ContentPublishing];
}

public static class SagaMessagingContract
{
    public const int CurrentSchemaVersion = 1;
    public const string PaymentExchange = "his-hope.payment";
    public const string ShipmentExchange = "his-hope.shipment";
    public const string IdentityExchange = "his-hope.identity";
    public const string ContentExchange = "his-hope.content";

    public const string PaymentAuthorized = "Commerce.PaymentAuthorized.v1";
    public const string PaymentAuthorizationRequested = "Commerce.PaymentAuthorizationRequested.v1";
    public const string PaymentCaptureRequested = "Commerce.PaymentCaptureRequested.v1";
    public const string PaymentRefundRequested = "Commerce.PaymentRefundRequested.v1";
    public const string PaymentCaptured = "Commerce.PaymentCaptured.v1";
    public const string PaymentRefunded = "Commerce.PaymentRefunded.v1";
    public const string ShipmentCreated = "Commerce.ShipmentCreated.v1";
    public const string ShipmentDispatched = "Commerce.ShipmentDispatched.v1";
    public const string ShipmentDelivered = "Commerce.ShipmentDelivered.v1";
    public const string TenantProvisioned = "Identity.TenantProvisioned.v1";
    public const string TenantProvisioningRequested = "Identity.TenantProvisioningRequested.v1";
    public const string ContentPublished = "Content.ContentPublished.v1";
    public const string ContentNotificationRequested = "Content.NotificationRequested.v1";
}

public sealed record PaymentAuthorizationRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, decimal Amount, string Currency, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);

public sealed record PaymentCaptureRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, string PaymentId, decimal Amount, string Currency,
    string IdempotencyKey, string? CorrelationId = null, string? CausationId = null);

public sealed record PaymentRefundRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, string PaymentId, decimal Amount, string Currency,
    string IdempotencyKey, string? CorrelationId = null, string? CausationId = null);

public sealed record PaymentResultV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, string PaymentId, decimal Amount, string Currency,
    string IdempotencyKey, string? FailureCode = null,
    string? CorrelationId = null, string? CausationId = null);

public sealed record ShipmentRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, string ShipmentId, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);

public sealed record ShipmentDeliveredV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid OrderId,
    string TenantKey, string ShipmentId, DateTimeOffset DeliveredAt,
    string? CorrelationId = null, string? CausationId = null);

public sealed record TenantProvisioningRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, string TenantKey,
    string DisplayName, string DataRegion, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);

public sealed record TenantProvisionedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, string TenantKey,
    Guid ScopeId, string DataRegion, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);

public sealed record ContentPublishedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid ArticleId,
    string TenantKey, string Locale, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);

public sealed record ContentNotificationRequestedV1(
    Guid EventId, int SchemaVersion, DateTimeOffset OccurredAt, Guid ArticleId,
    string TenantKey, string Locale, string IdempotencyKey,
    string? CorrelationId = null, string? CausationId = null);
