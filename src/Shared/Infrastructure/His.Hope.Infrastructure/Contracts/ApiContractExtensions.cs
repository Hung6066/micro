using His.Hope.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Contracts;

public static class ApiContractExtensions
{
    public static IServiceCollection AddHisHopeContractProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;
                var status = context.ProblemDetails.Status ?? httpContext.Response.StatusCode;
                var errorCode = context.ProblemDetails.Extensions.TryGetValue(
                    ApiProblemExtensions.ErrorCode, out var existingErrorCode)
                    ? existingErrorCode?.ToString() ?? ApiErrorCodes.ForStatus(status)
                    : ApiErrorCodes.ForStatus(status);
                context.ProblemDetails.Instance ??= httpContext.Request.Path;
                context.ProblemDetails.Extensions[ApiProblemExtensions.CorrelationId] =
                    GetCorrelationId(httpContext);
                context.ProblemDetails.Extensions.TryAdd(
                    ApiProblemExtensions.ErrorCode,
                    errorCode);

                var logger = httpContext.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger("HisHope.HttpErrors");
                var error = new ApiErrorLogEntry(
                    errorCode,
                    status,
                    "HTTP ProblemDetails response",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    GetCorrelationId(httpContext),
                    httpContext.TraceIdentifier,
                    status >= 500 ? null : context.ProblemDetails.Detail,
                    context.ProblemDetails is ValidationProblemDetails validation
                        ? validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)
                        : null);
                if (status >= 500)
                {
                    logger.LogError(
                        "HTTP error {@Error}", error);
                }
                else
                {
                    logger.LogWarning(
                        "HTTP error {@Error}", error);
                }
            };
        });

        return services;
    }

    public static Task WriteContractProblemAsync(
        this HttpContext context,
        int status,
        string title,
        string? detail = null,
        string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = $"https://his-hope.com/errors/{errorCode ?? ApiErrorCodes.ForStatus(status)}",
            Title = status >= 500 ? "The request could not be completed." : title,
            Status = status,
            Detail = status >= 500 ? null : detail,
            Instance = context.Request.Path
        };
        problem.Extensions[ApiProblemExtensions.CorrelationId] = GetCorrelationId(context);
        problem.Extensions[ApiProblemExtensions.ErrorCode] = errorCode ?? ApiErrorCodes.ForStatus(status);
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        return context.Response.WriteAsJsonAsync(problem);
    }

    private static string GetCorrelationId(HttpContext context) =>
        context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? context.TraceIdentifier;
}
