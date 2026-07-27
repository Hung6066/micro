using His.Hope.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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
                context.ProblemDetails.Instance ??= httpContext.Request.Path;
                context.ProblemDetails.Extensions[ApiProblemExtensions.CorrelationId] =
                    GetCorrelationId(httpContext);
                context.ProblemDetails.Extensions[ApiProblemExtensions.ErrorCode] =
                    ApiErrorCodes.ForStatus(status);
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
            Title = title,
            Status = status,
            Detail = detail,
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
