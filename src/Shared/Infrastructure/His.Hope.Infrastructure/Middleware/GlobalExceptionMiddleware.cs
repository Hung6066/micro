using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using His.Hope.Contracts;
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
        var (statusCode, title, errorCode) = MapException(exception);
        var correlationId = CorrelationContext.CurrentId
            ?? context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;

        if (statusCode >= 500)
        {
            var error = new ApiErrorLogEntry(
                errorCode,
                statusCode,
                exception.Message,
                context.Request.Method,
                context.Request.Path,
                correlationId,
                context.TraceIdentifier,
                null);
            _logger.LogError(exception,
                "HTTP error {@Error}", error);
        }
        else
        {
            var error = new ApiErrorLogEntry(
                errorCode,
                statusCode,
                exception.Message,
                context.Request.Method,
                context.Request.Path,
                correlationId,
                context.TraceIdentifier,
                Detail: exception.Message);
            _logger.LogWarning(exception,
                "HTTP error {@Error}", error);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var detail = statusCode >= 500 ? null : exception switch
        {
            ValidationException ve => string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)),
            _ => exception.Message
        };

        var problemDetails = new
        {
            type = $"https://his-hope.com/errors/{errorCode}",
            title = statusCode >= 500 ? "The request could not be completed." : title,
            status = statusCode,
            detail,
            instance = context.Request.GetDisplayUrl(),
            traceId = Activity.Current?.TraceId.ToString() ?? "unknown",
            correlationId,
            errorCode,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static (int statusCode, string title, string errorCode) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException => (400, "Bad Request", ApiErrorCodes.Validation),
            DomainException => (422, "Unprocessable Entity", ApiErrorCodes.UnprocessableEntity),
            NotFoundException => (404, "Not Found", ApiErrorCodes.NotFound),
            UnauthorizedException => (401, "Unauthorized", ApiErrorCodes.Unauthorized),
            ForbiddenException => (403, "Forbidden", ApiErrorCodes.Forbidden),
            _ => (500, "Internal Server Error", ApiErrorCodes.Internal)
        };
    }
}
