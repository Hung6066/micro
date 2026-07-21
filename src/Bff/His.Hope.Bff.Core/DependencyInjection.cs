using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Audit;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Resilience;
using His.Hope.Bff.Core.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace His.Hope.Bff.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddBffCore(
        this IServiceCollection services, IConfiguration configuration)
    {
        var cookieOptions = configuration
            .GetSection(SessionCookieOptions.SectionName)
            .Get<SessionCookieOptions>() ?? new SessionCookieOptions();

        services.AddSingleton(cookieOptions);

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:Connection"]
            ?? throw new InvalidOperationException("Redis connection string not configured");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddBffResilience();

        return services;
    }

    public static IApplicationBuilder UseBffCoreMiddleware(this IApplicationBuilder builder)
    {
        builder.UseBffSessionAuth();
        builder.UseBffMetrics();
        builder.UseBffCsrfProtection();
        builder.UseBffAudit(); // after auth (has userId), before proxy/aggregation
        return builder;
    }

    public static WebApplication MapBffAggregation(this WebApplication app)
    {
        var handlers = app.Services.GetServices<IAggregationHandler>();

        foreach (var handler in handlers)
        {
            app.MapMethods(handler.Route, new[] { handler.Method }, async (HttpContext context) =>
            {
                var routeValues = context.Request.RouteValues
                    .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                var jwt = context.Items["SessionJwt"] as string ?? "";

                var aggContext = new AggregationContext(
                    routeValues, jwt, context.RequestAborted);

                var result = await handler.HandleAsync(aggContext);

                context.Response.StatusCode = result.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    data = result.Data,
                    degraded = result.Degraded
                });
            });
        }

        return app;
    }
}
