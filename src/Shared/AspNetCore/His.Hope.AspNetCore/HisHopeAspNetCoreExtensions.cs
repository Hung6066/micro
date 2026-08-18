using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using His.Hope.Contracts;

namespace His.Hope.AspNetCore;

public static class HisHopeAspNetCoreExtensions
{
    internal const string CorrelationIdItemKey = "His.Hope.AspNetCore.CorrelationId";

    public static IServiceCollection AddHisHopeAspNetCore(
        this IServiceCollection services,
        Action<HisHopeAspNetCoreOptions>? configure = null)
    {
        var options = new HisHopeAspNetCoreOptions();
        configure?.Invoke(options);
        if (string.IsNullOrWhiteSpace(options.CorrelationHeaderName))
            throw new ArgumentException("CorrelationHeaderName must not be empty.", nameof(configure));
        if (options.MaximumCorrelationIdLength < 1)
            throw new ArgumentOutOfRangeException(nameof(configure), "MaximumCorrelationIdLength must be positive.");

        services.AddSingleton(options);
        services.AddProblemDetails(problemDetails =>
        {
            problemDetails.CustomizeProblemDetails = context =>
            {
                var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
                var correlationId = GetCorrelationId(context.HttpContext);
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
                if (!context.ProblemDetails.Extensions.ContainsKey(ApiProblemExtensions.ErrorCode))
                    context.ProblemDetails.Extensions[ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ForStatus(status);
                if (string.IsNullOrWhiteSpace(context.ProblemDetails.Type))
                    context.ProblemDetails.Type = $"https://his-hope.com/errors/{context.ProblemDetails.Extensions[ApiProblemExtensions.ErrorCode]}";
                if (status >= 500)
                {
                    context.ProblemDetails.Title = "The request could not be completed.";
                    context.ProblemDetails.Detail = null;
                }
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });
        services.AddHealthChecks();
        services.AddHisHopeOpenApi();

        return services;
    }

    public static IApplicationBuilder UseHisHopeAspNetCore(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseStatusCodePages(async statusContext =>
        {
            var httpContext = statusContext.HttpContext;
            if (httpContext.Response.HasStarted || httpContext.Response.ContentLength is not null ||
                httpContext.Response.StatusCode < 400)
                return;

            var status = httpContext.Response.StatusCode;
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = $"https://his-hope.com/errors/{ApiErrorCodes.ForStatus(status)}",
                Title = status >= 500 ? "The request could not be completed." : "The request failed.",
                Status = status,
                Instance = httpContext.Request.Path
            };
            problem.Extensions[ApiProblemExtensions.CorrelationId] = GetCorrelationId(httpContext);
            problem.Extensions[ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ForStatus(status);
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(problem);
        });
        return app;
    }

    public static IServiceCollection AddHisHopeOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        return services;
    }

    public static IEndpointRouteBuilder MapHisHopeHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string? path = null)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<HisHopeAspNetCoreOptions>();
        var healthPath = path ?? options.HealthPath;
        endpoints.MapHealthChecks(healthPath, new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        return endpoints;
    }

    public static string GetCorrelationId(this HttpContext context) =>
        GetCorrelationId(context, context.RequestServices.GetService<HisHopeAspNetCoreOptions>());

    private static string GetCorrelationId(HttpContext context, HisHopeAspNetCoreOptions? options = null) =>
        context.Items[CorrelationIdItemKey] as string
        ?? (options is not null ? context.Request.Headers[options.CorrelationHeaderName].FirstOrDefault() : null)
        ?? context.TraceIdentifier;

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new { status = entry.Value.Status.ToString(), description = entry.Value.Description })
        });
}
