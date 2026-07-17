using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using His.Hope.Infrastructure.Observability;
using His.Hope.SharedKernel.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, type) = MapException(exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with status {StatusCode}: {Message}", statusCode, exception.Message);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var detail = exception switch
        {
            ValidationException ve => string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)),
            _ => exception.Message
        };

        var problemDetails = new
        {
            type = $"https://his-hope.com/errors/{type}",
            title,
            status = statusCode,
            detail,
            instance = context.Request.GetDisplayUrl(),
            traceId = Activity.Current?.TraceId.ToString() ?? "unknown",
            correlationId = CorrelationContext.CurrentId,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static (int statusCode, string title, string type) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException => (400, "Bad Request", "validation-error"),
            DomainException => (422, "Unprocessable Entity", "domain-error"),
            NotFoundException => (404, "Not Found", "not-found"),
            UnauthorizedException => (401, "Unauthorized", "unauthorized"),
            ForbiddenException => (403, "Forbidden", "forbidden"),
            _ => (500, "Internal Server Error", "internal-server-error")
        };
    }
}
