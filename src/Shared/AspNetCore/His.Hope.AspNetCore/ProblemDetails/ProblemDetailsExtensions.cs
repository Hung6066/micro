using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AspNetCore.ProblemDetails;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddHisHopeProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var httpContext = context.HttpContext;
                context.ProblemDetails.Instance ??= httpContext.Request.Path;
                context.ProblemDetails.Extensions["correlationId"] =
                    HisHopeCorrelation.GetId(httpContext);
            };
        });
        return services;
    }

    public static Task WriteHisHopeProblemAsync(
        this HttpContext context,
        int status,
        string title,
        string? detail = null,
        string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = $"https://his-hope.com/errors/{errorCode ?? StatusCodeToErrorCode(status)}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = HisHopeCorrelation.GetId(context);
        problem.Extensions["errorCode"] = errorCode ?? StatusCodeToErrorCode(status);
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(problem));
    }

    private static string StatusCodeToErrorCode(int status) => status switch
    {
        400 => "bad-request",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not-found",
        409 => "conflict",
        422 => "validation-error",
        _ when status >= 500 => "internal-server-error",
        _ => "request-failed"
    };
}
