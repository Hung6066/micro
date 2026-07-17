using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// ASP.NET middleware that enforces idempotent processing of
/// POST / PUT / PATCH requests via the <c>Idempotency-Key</c> header.
///
/// Design Rationale:
///   In a hospital information system, network issues, client timeouts, and
///   service mesh retries can cause the same mutating request to arrive more
///   than once. Without idempotency, this could lead to duplicate charge
///   entries, double-booked appointments, or duplicate lab orders – all of
///   which would harm patient care and billing integrity.
///
/// How it works:
///   1. On the first request for a given key, the middleware inserts a
///      "Processing" record, delegates to the next middleware, then updates
///      the record to "Completed" with the response status and body.
///   2. On a subsequent request with the same key AND the same request body
///      (SHA-256 hash), the cached response is returned directly – the
///      downstream service is never called again.
///   3. On a subsequent request with the same key BUT a DIFFERENT body,
///      a 409 Conflict is returned because the key was already claimed by
///      a different payload.
///   4. If a record is still "Processing" (e.g. the first request is still
///      in-flight), a second concurrent request gets 409 Conflict.
///
/// HIPAA Context (164.312(a)(2)(iv)):
///   Idempotency also serves as an integrity control – it prevents
///   accidental duplicate mutations to PHI records and provides an
///   audit trail of retried requests through the idempotency_keys table.
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    // Only these HTTP methods are candidates for idempotency checking
    private static readonly HashSet<string> IdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "PATCH" };

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IdempotencyDbContext dbContext)
    {
        // Skip non-mutating requests
        if (!IdempotentMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip health checks and other infrastructure endpoints
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            // No idempotency key provided – let the request pass through
            // This maintains backward compatibility with non-idempotent clients
            await _next(context);
            return;
        }

        // Validate key format – reject overly long keys
        if (idempotencyKey.Length > 255)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Idempotency-Key must not exceed 255 characters"}""",
                Encoding.UTF8);
            return;
        }

        // Read and hash the request body (enable buffering so downstream can read it too)
        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
        }
        context.Request.Body.Position = 0;

        var requestHash = ComputeSha256Hash(requestBody);

        // Extract service and endpoint info for the record
        var serviceName = context.Request.Host.Host ?? "gateway";
        var endpoint = context.Request.Path.Value ?? "";
        var httpMethod = context.Request.Method;

        // Look for an existing idempotency key record
        var existing = await dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.IdempotencyKeyValue == idempotencyKey);

        if (existing != null)
        {
            if (existing.Status == "Processing")
            {
                // First request is still being processed – second concurrent request must wait
                _logger.LogWarning(
                    "Idempotency key {Key} is still in Processing state for {Method} {Path}",
                    idempotencyKey, httpMethod, endpoint);

                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = "5";
                await context.Response.WriteAsync(
                    """{"error":"Request is still being processed","status":"Processing"}""",
                    Encoding.UTF8);
                return;
            }

            if (existing.Status == "Completed")
            {
                // Same key, same hash → idempotent replay, return cached response
                if (existing.RequestHash == requestHash)
                {
                    _logger.LogInformation(
                        "Idempotent replay detected for key {Key} on {Method} {Path} – returning cached response {StatusCode}",
                        idempotencyKey, httpMethod, endpoint, existing.ResponseStatusCode);

                    context.Response.StatusCode = existing.ResponseStatusCode ?? 200;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Idempotent-Replayed"] = "true";

                    if (!string.IsNullOrEmpty(existing.ResponseBody))
                    {
                        await context.Response.WriteAsync(existing.ResponseBody, Encoding.UTF8);
                    }
                    return;
                }

                // Same key, DIFFERENT hash → key conflict
                _logger.LogWarning(
                    "Idempotency key collision: key {Key} already used with different request body for {Method} {Path}",
                    idempotencyKey, httpMethod, endpoint);

                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"error":"Idempotency-Key already used with a different request body","status":"Conflict"}""",
                    Encoding.UTF8);
                return;
            }

            // Unknown status – let it pass (defensive)
            _logger.LogWarning(
                "Idempotency key {Key} has unexpected status '{Status}' – allowing passthrough",
                idempotencyKey, existing.Status);
            await _next(context);
            return;
        }

        // ====================================================================
        // No existing record – this is the first attempt.
        // Insert a Processing record, execute the request, then mark Completed.
        // ====================================================================

        var record = new IdempotencyKey
        {
            IdempotencyKeyValue = idempotencyKey,
            ServiceName = serviceName,
            Endpoint = endpoint,
            HttpMethod = httpMethod,
            RequestHash = requestHash,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        dbContext.IdempotencyKeys.Add(record);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            // Race condition: another request beat us to inserting the same key.
            // This shouldn't normally happen since we already checked above,
            // but account for the tiny window between check and insert.
            _logger.LogWarning(
                ex,
                "Race condition on idempotency key {Key} – concurrent request inserted first",
                idempotencyKey);

            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Request is being processed","status":"Processing"}""",
                Encoding.UTF8);
            return;
        }

        // Capture the response so we can cache it
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        Exception? processingException = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            processingException = ex;
        }

        // Restore original body stream
        context.Response.Body = originalBodyStream;

        if (processingException != null)
        {
            // The downstream pipeline threw – mark the record as Failed so the
            // client can retry with confidence the server state wasn't committed.
            record.Status = "Failed";
            record.ResponseStatusCode = 500;
            record.ResponseBody = null;

            try
            {
                dbContext.IdempotencyKeys.Update(record);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx,
                    "Failed to update idempotency key {Key} status to Failed after exception",
                    idempotencyKey);
            }

            _logger.LogError(processingException,
                "Request with idempotency key {Key} failed – status set to Failed",
                idempotencyKey);

            throw processingException; // Re-throw so the exception middleware can handle it
        }

        // Read the captured response
        responseBodyStream.Position = 0;
        string responseBody;
        using (var reader = new StreamReader(responseBodyStream, Encoding.UTF8))
        {
            responseBody = await reader.ReadToEndAsync();
        }

        // Copy the captured response to the original stream
        responseBodyStream.Position = 0;
        await responseBodyStream.CopyToAsync(originalBodyStream);

        // Update the idempotency record to Completed
        record.Status = "Completed";
        record.ResponseStatusCode = context.Response.StatusCode;
        record.ResponseBody = responseBody;

        try
        {
            dbContext.IdempotencyKeys.Update(record);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update idempotency key {Key} to Completed",
                idempotencyKey);
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string and returns it as a
    /// lowercase hex string (64 characters).
    /// </summary>
    private static string ComputeSha256Hash(string rawData)
    {
        if (string.IsNullOrEmpty(rawData))
            rawData = string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
