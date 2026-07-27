using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
            logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
                exception, "Request failed with status {StatusCode}", status);

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
