using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using His.Hope.Contracts;
using His.Hope.SharedKernel.Protocol;

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
                var status = context.ProblemDetails.Status ?? httpContext.Response.StatusCode;
                var errorCode = context.ProblemDetails.Extensions.TryGetValue(
                    ApiProblemExtensions.ErrorCode, out var existingErrorCode)
                    ? existingErrorCode?.ToString() ?? ApiErrorCodes.ForStatus(status)
                    : ApiErrorCodes.ForStatus(status);
                context.ProblemDetails.Instance ??= httpContext.Request.Path;
                context.ProblemDetails.Extensions[HisHopeProtocolConstants.Claims.CorrelationId] =
                    HisHopeCorrelation.GetId(httpContext);
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
                    HisHopeCorrelation.GetId(httpContext),
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
            Title = status >= 500 ? "The request could not be completed." : title,
            Status = status,
            Detail = status >= 500 ? null : detail,
            Instance = context.Request.Path
        };
        problem.Extensions[HisHopeProtocolConstants.Claims.CorrelationId] = HisHopeCorrelation.GetId(context);
        problem.Extensions["errorCode"] = errorCode ?? StatusCodeToErrorCode(status);
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        context.Response.StatusCode = status;
        context.Response.ContentType = HisHopeProtocolConstants.MediaTypes.ProblemJson;
        return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(problem));
    }

    private static string StatusCodeToErrorCode(int status) => status switch
    {
        400 => ApiErrorCodes.Validation,
        401 => ApiErrorCodes.Unauthorized,
        403 => ApiErrorCodes.Forbidden,
        404 => ApiErrorCodes.NotFound,
        409 => ApiErrorCodes.Conflict,
        422 => ApiErrorCodes.UnprocessableEntity,
        _ when status >= 500 => ApiErrorCodes.Internal,
        _ => ApiErrorCodes.ForStatus(status)
    };
}
