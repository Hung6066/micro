using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.CommerceService.Application;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Secrets;
using His.Hope.ServiceDefaults;
using Microsoft.Extensions.Options;

namespace His.Hope.CommerceService.Api;

internal static class CommerceWebhookEndpoints
{
    public static void MapCommerceWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/commerce/webhooks/shipment-delivery", async (
            HttpRequest request,
            CommerceShipmentWorkflow workflow,
            IOptions<ShipmentProviderOptions> options,
            IVaultSecretProvider secrets,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(options.Value.WebhookSecretPath))
                return Results.Problem("Shipment webhook is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            var signature = request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var body = await new StreamReader(request.Body).ReadToEndAsync(ct);
            var secret = await secrets.GetAsync(options.Value.WebhookSecretPath, options.Value.WebhookSecretKey, ct);
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
                return Results.Unauthorized();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant())))
                return Results.Unauthorized();
            var payload = JsonSerializer.Deserialize<ShipmentDeliveryWebhook>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is null || string.IsNullOrWhiteSpace(payload.TenantKey) || string.IsNullOrWhiteSpace(payload.ProviderShipmentId))
                return Results.BadRequest();
            var delivered = await workflow.MarkDeliveredAsync(payload.TenantKey, payload.ProviderShipmentId, ct);
            return delivered ? Results.NoContent() : Results.NotFound();
        }).AllowAnonymous();
    }
}
