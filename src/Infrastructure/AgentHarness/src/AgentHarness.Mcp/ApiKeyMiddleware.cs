using Serilog;

namespace His.Hope.AgentHarness.Mcp;

/// <summary>
/// Middleware that validates X-API-Key header on all endpoints except /health and /mcp/sse.
/// The API key is configured via AgentHarness__ApiKey environment variable.
/// If no key is configured, authentication is skipped (development mode).
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _configuredKey;

    private static readonly string[] AllowedPaths = { "/health", "/mcp/sse" };

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _configuredKey = config["AgentHarness:ApiKey"] ?? config["AgentHarness__ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for allowed paths or when no key is configured
        if (string.IsNullOrEmpty(_configuredKey) ||
            AllowedPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey) || apiKey != _configuredKey)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized. Provide X-API-Key header.\"}");
            return;
        }

        await _next(context);
    }
}
