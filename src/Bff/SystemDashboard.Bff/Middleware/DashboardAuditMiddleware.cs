using System.Security.Claims;
using System.Threading.Channels;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Middleware;

public sealed class DashboardAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ChannelWriter<AuditEvent> _writer;

    public DashboardAuditMiddleware(RequestDelegate next, Channel<AuditEvent> channel)
    {
        _next = next;
        _writer = channel.Writer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = context.User.FindFirstValue("name") ?? "";
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "";
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        await _next(context);

        sw.Stop();

        if (isAuthenticated && userId is not null)
        {
            var auditEvent = new AuditEvent
            {
                UserId = userId,
                UserName = userName,
                Role = role,
                Action = DeriveAction(context.Request.Path, context.Request.Method),
                Resource = context.Request.Path,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                StatusCode = context.Response.StatusCode
            };

            _writer.TryWrite(auditEvent);
        }
    }

    private static string DeriveAction(string path, string method) => (path, method) switch
    {
        var (p, _) when p.StartsWith("/api/resources") => "resource_view",
        var (p, _) when p.StartsWith("/api/metrics") => "query_metrics",
        var (p, _) when p.StartsWith("/api/logs") => "query_logs",
        var (p, _) when p.StartsWith("/api/traces") => "query_traces",
        _ => "page_view"
    };
}
