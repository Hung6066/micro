using His.Hope.Bff.Core.Aggregation;
using His.Hope.Bff.Core.Audit;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Bff.Core.Resilience;
using His.Hope.Bff.Core.Telemetry;
using His.Hope.AspNetCore;
using His.Hope.Configuration;
using His.Hope.Observability;
using His.Hope.Infrastructure.Caching;
using His.Hope.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Grpc.Core;

namespace His.Hope.Bff.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddBffCore(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var runtimeEndpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, serviceName);
        services.AddHisHopeRuntimeConfiguration(configuration, serviceName);
        services.AddHisHopeServiceToServiceAuthentication(configuration);
        services.AddHisHopeAspNetCore();
        services.AddHealthChecks();
        services.AddObservability(options =>
            options.ServiceName = configuration["ServiceName"] ?? "His.Hope.Bff");

        var cookieOptions = configuration
            .GetSection(SessionCookieOptions.SectionName)
            .Get<SessionCookieOptions>() ?? new SessionCookieOptions();

        services.AddSingleton(cookieOptions);

        var redisConnection = RuntimeConfigurationExtensions.ToRedisConnectionString(
            runtimeEndpoints.GetRequired("redis"));

        var redis = RedisConnectionFactory.Connect(redisConnection, configuration);
        services.AddSingleton<IConnectionMultiplexer>(redis);
        var dataProtectionKeyName = configuration[HisHopeConfigurationKeys.DataProtectionKeyName]
            ?? "HisHope:IdentityService:DataProtection:Keys";
        services.AddDataProtection()
            .SetApplicationName("His.Hope.IdentityService")
            .PersistKeysToStackExchangeRedis(redis, dataProtectionKeyName);
        services.AddSingleton<SessionTokenProtector>();

        services.AddBffResilience();

        return services;
    }

    public static IApplicationBuilder UseBffCoreMiddleware(this IApplicationBuilder builder)
    {
        builder.UseHisHopeAspNetCore();
        builder.UseBffSessionAuth();
        builder.UseBffMetrics();
        builder.UseBffCsrfProtection();
        builder.UseBffAudit(); // after auth (has userId), before proxy/aggregation
        return builder;
    }

    public static WebApplication MapBffAggregation(this WebApplication app)
    {
        // Production handlers are singletons. Some integration hosts intentionally
        // register them as scoped; support both lifetimes while keeping route metadata
        // available during endpoint mapping.
        IServiceScope? mappingScope = null;
        IEnumerable<IAggregationHandler> handlers;
        try
        {
            handlers = app.Services.GetServices<IAggregationHandler>().ToArray();
        }
        catch (InvalidOperationException)
        {
            mappingScope = app.Services.CreateScope();
            handlers = mappingScope.ServiceProvider.GetServices<IAggregationHandler>().ToArray();
            app.Lifetime.ApplicationStopped.Register(mappingScope.Dispose);
        }

        foreach (var handler in handlers)
        {
            app.MapMethods(handler.Route, new[] { handler.Method }, async (HttpContext context) =>
            {
                var routeValues = context.Request.RouteValues
                    .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                var jwt = context.Items["SessionJwt"] as string ?? "";

                var aggContext = new AggregationContext(
                    routeValues, jwt, context.RequestAborted);

                AggregationResult result;
                try
                {
                    result = await handler.HandleAsync(aggContext);
                }
                catch (Exception exception) when (IsDownstreamFailure(exception))
                {
                    // Aggregation endpoints are a boundary around downstream services.
                    // Never expose transport exceptions as a 500; return a stable gateway
                    // response so clients can retry or render a degraded state.
                    app.Logger.LogWarning(exception,
                        "Downstream aggregation failed for {Route}", handler.Route);
                    result = AggregationResult.Failed("Downstream service unavailable");
                }

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

    private static bool IsDownstreamFailure(Exception exception) => exception switch
    {
        RpcException rpc when rpc.StatusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.Internal
            or StatusCode.Unknown => true,
        HttpRequestException => true,
        TimeoutException => true,
        _ => exception.InnerException is not null && IsDownstreamFailure(exception.InnerException)
    };

    public static WebApplication MapBffHealth(this WebApplication app)
    {
        app.MapHisHopeHealthEndpoints();
        return app;
    }
}
