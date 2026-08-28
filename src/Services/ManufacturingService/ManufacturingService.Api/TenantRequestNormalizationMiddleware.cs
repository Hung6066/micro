using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using His.Hope.AspNetCore.Tenancy;

/// <summary>
/// Compatibility bridge for the tenant-context contract migration.
/// New clients send X-HisHope-Tenant and omit tenantKey from command bodies;
/// legacy clients may continue sending tenantKey while endpoint guards validate
/// it against the authenticated tenant context.
/// </summary>
internal sealed class TenantRequestNormalizationMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldNormalize(context.Request))
        {
            await next(context);
            return;
        }

        var tenantKey = context.ResolveActiveTenant();
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(context.RequestAborted);
        context.Request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(payload))
        {
            await next(context);
            return;
        }

        JsonNode? node;
        try { node = JsonNode.Parse(payload); }
        catch (JsonException)
        {
            await next(context);
            return;
        }

        if (node is JsonObject body && !body.Any(property => string.Equals(property.Key, "tenantKey", StringComparison.OrdinalIgnoreCase)))
        {
            body["tenantKey"] = tenantKey;
            var normalized = JsonSerializer.Serialize(body, JsonOptions);
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(normalized));
            context.Request.ContentLength = Encoding.UTF8.GetByteCount(normalized);
        }
        else
        {
            context.Request.Body.Position = 0;
        }

        await next(context);
    }

    private static bool ShouldNormalize(HttpRequest request) =>
        request.Path.StartsWithSegments("/api/v1/manufacturing") &&
        request.Method is "POST" or "PUT" or "PATCH" &&
        request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
}
