namespace His.Hope.IdentityService.Api.Metrics;

public class SloMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SloMiddleware> _logger;

    public SloMiddleware(RequestDelegate next, ILogger<SloMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        if (path?.StartsWith("/connect/token") == true)
        {
            using (IdentitySloMetrics.MeasureTokenIssue())
            {
                await _next(context);

                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                    IdentitySloMetrics.RecordTokenIssued("authorization_code");
                else
                    IdentitySloMetrics.RecordTokenFailure("authorization_code", $"http_{context.Response.StatusCode}");
            }
        }
        else if (path?.StartsWith("/connect/introspect") == true)
        {
            using (IdentitySloMetrics.MeasureIntrospection())
            {
                await _next(context);
                IdentitySloMetrics.RecordIntrospection();
            }
        }
        else if (path?.StartsWith("/api/v1/auth/login") == true)
        {
            using (IdentitySloMetrics.MeasureLogin())
            {
                await _next(context);
                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                    IdentitySloMetrics.RecordLoginSucceeded("password");
                else
                    IdentitySloMetrics.RecordLoginFailed($"http_{context.Response.StatusCode}");
            }
        }
        else
        {
            await _next(context);
        }
    }
}
