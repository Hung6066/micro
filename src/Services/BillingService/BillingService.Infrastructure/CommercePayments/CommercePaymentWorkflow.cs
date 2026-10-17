using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Outbox;
using His.Hope.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.BillingService.Infrastructure.CommercePayments;

public static class CommercePaymentStates
{
    public const string Pending = "pending";
    public const string Authorized = "authorized";
    public const string Captured = "captured";
    public const string Refunded = "refunded";
    public const string Failed = "failed";
}

public sealed class CommercePaymentEntity
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string State { get; set; } = CommercePaymentStates.Pending;
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CommercePaymentWorkflow(
    BillingDbContext db,
    IPaymentProvider provider)
{
    public async Task AuthorizeAsync(PaymentAuthorizationRequestedV1 request, CancellationToken ct)
    {
        var payment = await db.CommercePayments
            .SingleOrDefaultAsync(x => x.TenantKey == request.TenantKey && x.OrderId == request.OrderId, ct);
        if (payment?.State is CommercePaymentStates.Authorized or CommercePaymentStates.Captured)
            return;

        var isNew = payment is null;
        payment ??= new CommercePaymentEntity
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            TenantKey = request.TenantKey,
            Amount = request.Amount,
            Currency = request.Currency,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        if (isNew)
            db.CommercePayments.Add(payment);

        var result = await provider.AuthorizeAsync(
            new PaymentProviderRequest(request.OrderId, request.TenantKey, request.Amount,
                request.Currency, request.IdempotencyKey, payment.ProviderPaymentId), ct);
        if (!result.Succeeded)
        {
            payment.State = CommercePaymentStates.Failed;
            payment.FailureCode = result.FailureCode;
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException(result.FailureCode ?? "payment_authorization_failed");
        }

        payment.State = CommercePaymentStates.Authorized;
        payment.ProviderPaymentId = result.ProviderPaymentId;
        payment.FailureCode = null;
        payment.UpdatedAt = DateTime.UtcNow;
        AddResultOutbox(SagaMessagingContract.PaymentAuthorized, request, result.ProviderPaymentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task CaptureAsync(PaymentResultV1 request, CancellationToken ct)
    {
        var payment = await FindRequiredAsync(request, ct);
        if (payment.State is CommercePaymentStates.Captured or CommercePaymentStates.Refunded)
            return;
        if (payment.State != CommercePaymentStates.Authorized)
            throw new InvalidOperationException("payment_capture_requires_authorization");

        var result = await provider.CaptureAsync(
            new PaymentProviderRequest(request.OrderId, request.TenantKey, request.Amount,
                request.Currency, request.IdempotencyKey, payment.ProviderPaymentId), ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.FailureCode ?? "payment_capture_failed");
        payment.State = CommercePaymentStates.Captured;
        payment.ProviderPaymentId = result.ProviderPaymentId;
        payment.UpdatedAt = DateTime.UtcNow;
        AddResultOutbox(SagaMessagingContract.PaymentCaptured, request, result.ProviderPaymentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task RefundAsync(PaymentResultV1 request, CancellationToken ct)
    {
        var payment = await FindRequiredAsync(request, ct);
        if (payment.State == CommercePaymentStates.Refunded)
            return;
        if (payment.State is not (CommercePaymentStates.Authorized or CommercePaymentStates.Captured))
            throw new InvalidOperationException("payment_refund_requires_authorization");

        var result = await provider.RefundAsync(
            new PaymentProviderRequest(request.OrderId, request.TenantKey, request.Amount,
                request.Currency, request.IdempotencyKey, payment.ProviderPaymentId), ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.FailureCode ?? "payment_refund_failed");
        payment.State = CommercePaymentStates.Refunded;
        payment.UpdatedAt = DateTime.UtcNow;
        AddResultOutbox(SagaMessagingContract.PaymentRefunded, request, result.ProviderPaymentId);
        await db.SaveChangesAsync(ct);
    }

    private async Task<CommercePaymentEntity> FindRequiredAsync(PaymentResultV1 request, CancellationToken ct) =>
        await db.CommercePayments.SingleOrDefaultAsync(
            x => x.TenantKey == request.TenantKey && x.OrderId == request.OrderId, ct)
        ?? throw new InvalidOperationException("commerce_payment_not_found");

    private void AddResultOutbox(string type, PaymentResultV1 request, string providerPaymentId)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = type,
            Content = JsonSerializer.Serialize(new PaymentResultV1(
                Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
                request.OrderId, request.TenantKey, providerPaymentId, request.Amount,
                request.Currency, request.IdempotencyKey, request.FailureCode,
                request.CorrelationId, request.CausationId)),
            CorrelationId = request.CorrelationId,
            CausationId = request.CausationId,
        });
    }

    private void AddResultOutbox(string type, PaymentAuthorizationRequestedV1 request, string providerPaymentId) =>
        AddResultOutbox(type, new PaymentResultV1(
            Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
            request.OrderId, request.TenantKey, providerPaymentId, request.Amount,
            request.Currency, request.IdempotencyKey, CorrelationId: request.CorrelationId,
            CausationId: request.CausationId), providerPaymentId);
}
