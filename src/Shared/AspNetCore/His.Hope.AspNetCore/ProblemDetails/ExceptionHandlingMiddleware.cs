using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using His.Hope.SharedKernel.Domain.Exceptions;
using His.Hope.Contracts;

namespace His.Hope.AspNetCore.ProblemDetails;

internal sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var status = exception switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                DomainException => StatusCodes.Status422UnprocessableEntity,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
            var correlationId = HisHopeCorrelation.GetId(context);
            var error = new ApiErrorLogEntry(
                ApiErrorCodes.ForStatus(status),
                status,
                exception.Message,
                context.Request.Method,
                context.Request.Path,
                correlationId,
                context.TraceIdentifier,
                status >= 500 ? null : exception.Message);
            logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
                exception, "HTTP error {@Error}", error);

            var detail = status >= 500 ? "An unexpected error occurred." : exception.Message;
            await context.WriteHisHopeProblemAsync(status, status >= 500
                ? "Internal Server Error"
                : "Request Failed", detail);
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseHisHopeExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
