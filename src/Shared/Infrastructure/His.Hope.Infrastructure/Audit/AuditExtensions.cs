using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Audit;

/// <summary>
/// Extension methods for registering PHI audit services and middleware.
/// </summary>
public static class AuditExtensions
{
    /// <summary>
    /// Registers the PHI audit service and middleware in the DI container.
    ///
    /// Usage in Program.cs:
    ///   builder.Services.AddPhiAudit();
    ///   app.UsePhiAudit();
    /// </summary>
    public static IServiceCollection AddPhiAudit(this IServiceCollection services)
    {
        // Register the audit service as singleton for performance
        // SECURITY: Thread-safe design allows shared usage across all requests
        services.AddSingleton<IAuditService, AuditService>();

        return services;
    }

    /// <summary>
    /// Adds the PHI audit middleware to the request pipeline.
    /// Should be placed early in the pipeline, after authentication
    /// but before endpoint handlers.
    /// </summary>
    public static IApplicationBuilder UsePhiAudit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditMiddleware>();
    }
}
