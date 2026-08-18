using FluentValidation;
using His.Hope.Contracts;
using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.AspNetCore.ProblemDetails;
using His.Hope.SharedKernel.Domain.Exceptions;
using NotFoundException = His.Hope.SharedKernel.Domain.Exceptions.NotFoundException;

namespace His.Hope.PharmacyService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        var statusCode = StatusCodes.Status500InternalServerError;
        string title = "The request could not be completed.";
        string? detail = null;
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "The request is invalid.";
                detail = "Validation failed.";
                errors = validationEx.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
                break;

            case DomainException domainEx:
                statusCode = StatusCodes.Status422UnprocessableEntity;
                title = "The request could not be processed.";
                detail = domainEx.Message;
                break;

            case NotFoundException notFoundEx:
                statusCode = StatusCodes.Status404NotFound;
                title = "The requested resource was not found.";
                detail = notFoundEx.Message;
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;
        var error = new ApiErrorLogEntry(
            ApiErrorCodes.ForStatus(statusCode),
            statusCode,
            exception.Message,
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.TraceIdentifier,
            statusCode >= 500 ? null : detail);
        _logger.Log(statusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception, "HTTP error {@Error}", error);

        await context.WriteHisHopeProblemAsync(statusCode, title, detail, errors: errors);
    }
}
