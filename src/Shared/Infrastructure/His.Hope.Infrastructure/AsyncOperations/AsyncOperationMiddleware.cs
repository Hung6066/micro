using System.Net;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// ASP.NET middleware that implements the async request-reply pattern
/// for long-running operations.
///
/// When a POST or PUT request includes the <c>Prefer: respond-async</c>
/// header, the middleware:
/// <list type="number">
///   <item>Creates an <see cref="OperationStatus"/> record (Status = Queued).</item>
///   <item>Returns HTTP 202 Accepted with a <c>Location</c> header pointing to
///         <c>/api/v1/operations/{id}</c>.</item>
///   <item>Enqueues the work to a <see cref="Channel{T}"/> for background processing.</item>
/// </list>
///
/// Clients poll <c>GET /api/v1/operations/{id}</c> to track progress and
/// retrieve the result when the operation completes.
/// </summary>
public class AsyncOperationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AsyncOperationMiddleware> _logger;

    // Only POST and PUT requests are candidates for async processing
    private static readonly HashSet<string> AsyncCapableMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT" };

    public AsyncOperationMiddleware(RequestDelegate next, ILogger<AsyncOperationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        OperationStatusDbContext dbContext,
        ChannelWriter<AsyncOperationWorkItem> channelWriter)
    {
        // Only intercept POST/PUT
        if (!AsyncCapableMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Check for Prefer: respond-async header
        var preferHeader = context.Request.Headers["Prefer"].FirstOrDefault();
        if (string.IsNullOrEmpty(preferHeader) ||
            !preferHeader.Contains("respond-async", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Skip health checks and metrics
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/metrics")
            || context.Request.Path.StartsWithSegments("/api/v1/operations"))
        {
            await _next(context);
            return;
        }

        // Read the request body
        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
        }
        context.Request.Body.Position = 0;

        // Determine the operation type from the path
        var operationType = DeriveOperationType(context.Request.Path, context.Request.Method);

        // Create the OperationStatus record
        var operationId = Guid.NewGuid();
        var record = new OperationStatus
        {
            Id = operationId,
            OperationType = operationType,
            Status = OperationStatusValue.Queued,
            Progress = 0,
            RequestData = requestBody,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        dbContext.OperationStatuses.Add(record);
        await dbContext.SaveChangesAsync(context.RequestAborted);

        // Enqueue the work item
        var workItem = new AsyncOperationWorkItem
        {
            OperationId = operationId,
            OperationType = operationType,
            RequestData = requestBody
        };

        await channelWriter.WriteAsync(workItem, context.RequestAborted);

        _logger.LogInformation(
            "Async operation {OperationId} ({Type}) queued for {Method} {Path}",
            operationId, operationType, context.Request.Method, context.Request.Path);

        // Return 202 Accepted with Location header
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
        context.Response.Headers["Location"] = $"/api/v1/operations/{operationId}";
        context.Response.ContentType = "application/json";

        var responseBody = JsonConvert.SerializeObject(new
        {
            operationId,
            status = OperationStatusValue.Queued,
            location = $"/api/v1/operations/{operationId}"
        });

        await context.Response.WriteAsync(responseBody, Encoding.UTF8, context.RequestAborted);
    }

    /// <summary>
    /// Derives a stable operation type string from the HTTP path and method.
    /// Example: POST /api/v1/patients/import → "PatientImport"
    /// </summary>
    private static string DeriveOperationType(PathString path, string method)
    {
        // Take the last meaningful segment(s) of the path and combine with method
        var segments = (path.Value ?? "")
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Filter out version prefixes and "api" to get the meaningful parts
        var meaningful = segments
            .Where(s => !s.Equals("api", StringComparison.OrdinalIgnoreCase)
                        && !s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Use the last 1-2 segments as the operation type name
        var typeName = meaningful.Count >= 2
            ? string.Concat(meaningful[^2], meaningful[^1])
            : meaningful.LastOrDefault() ?? "Operation";

        // PascalCase the result
        return char.ToUpperInvariant(typeName[0]) + typeName[1..];
    }
}
