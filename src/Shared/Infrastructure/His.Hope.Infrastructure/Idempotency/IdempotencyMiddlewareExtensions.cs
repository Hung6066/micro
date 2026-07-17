using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// Extension methods for registering the idempotency middleware
/// and its backing DbContext in the ASP.NET pipeline.
/// </summary>
public static class IdempotencyMiddlewareExtensions
{
    private const string DefaultConnectionStringName = "IdempotencyDb";

    /// <summary>
    /// Adds the <see cref="IdempotencyDbContext"/> to the service collection,
    /// configured with the connection string named <c>IdempotencyDb</c> from
    /// the application's configuration.
    /// </summary>
    public static IServiceCollection AddIdempotency(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DefaultConnectionStringName);
        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback to the IdentityDb connection string as a convenience
            // for environments that share a database (e.g., local dev).
            connectionString = configuration.GetConnectionString("IdentityDb");
        }

        services.AddDbContext<IdempotencyDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                b =>
                {
                    b.MigrationsAssembly(typeof(IdempotencyDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                }));

        return services;
    }

    /// <summary>
    /// Adds the idempotency middleware to the ASP.NET pipeline.
    /// Should be placed before authentication and after rate limiting
    /// to catch idempotent replays early.
    /// </summary>
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}
