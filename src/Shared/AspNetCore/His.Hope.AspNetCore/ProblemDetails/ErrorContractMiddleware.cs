using System.Text.Json;
using His.Hope.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace His.Hope.AspNetCore.ProblemDetails;

/// <summary>
/// Keeps legacy minimal-api error bodies compatible with the shared
/// application/problem+json contract. Existing endpoints may still return
/// { error: "stable_code" }; this middleware upgrades that response at the
/// HTTP boundary without exposing server exceptions or changing success data.
/// </summary>
internal sealed class ErrorContractMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // gRPC owns the response stream and trailers. Buffering it for the
        // REST error-shape adapter strips the unary message body even when
        // the HTTP status is 200. Keep both native gRPC and grpc-web on the
        // original stream; only REST responses need normalization here.
        if (context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var capturedBody = new MemoryStream();
        context.Response.Body = capturedBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        capturedBody.Position = 0;

        if (!ShouldNormalize(context.Response, capturedBody))
        {
            context.Response.ContentLength = null;
            await capturedBody.CopyToAsync(originalBody);
            return;
        }

        JsonElement payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<JsonElement>(capturedBody);
        }
        catch (JsonException)
        {
            capturedBody.Position = 0;
            context.Response.ContentLength = null;
            await capturedBody.CopyToAsync(originalBody);
            return;
        }
        if (payload.ValueKind != JsonValueKind.Object ||
            !TryReadCode(payload, out var errorCode))
        {
            capturedBody.Position = 0;
            context.Response.ContentLength = null;
            await capturedBody.CopyToAsync(originalBody);
            return;
        }

        // Preserve a fully formed contract response (including validation
        // errors) instead of dropping its additional fields.
        if (payload.TryGetProperty("type", out _) &&
            payload.TryGetProperty("errorCode", out _))
        {
            capturedBody.Position = 0;
            context.Response.ContentLength = null;
            context.Response.ContentType = "application/problem+json";
            await capturedBody.CopyToAsync(originalBody);
            return;
        }

        var status = context.Response.StatusCode;
        // The legacy body is still in the detached capture stream, so do not
        // call Response.Clear() here: once a response has started that call
        // may be ignored, leaving the legacy content type in place.
        // Replace the captured body by writing the normalized response to the
        // original stream and explicitly preserve the error status.
        context.Response.StatusCode = status;
        context.Response.ContentLength = null;
        context.Response.ContentType = "application/problem+json";
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = $"https://his-hope.com/errors/{errorCode}",
            Title = status >= 500 ? "The request could not be completed." : "The request failed.",
            Status = status,
            Instance = context.Request.Path
        };
        problem.Extensions[ApiProblemExtensions.ErrorCode] = errorCode;
        problem.Extensions[ApiProblemExtensions.CorrelationId] =
            context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static bool ShouldNormalize(HttpResponse response, MemoryStream body) =>
        response.StatusCode >= 400 && body.Length > 0;

    private static bool TryReadCode(JsonElement payload, out string code)
    {
        foreach (var name in new[] { "errorCode", "code", "error" })
        {
            if (payload.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                code = value.GetString()!;
                return true;
            }
        }

        code = string.Empty;
        return false;
    }
}
