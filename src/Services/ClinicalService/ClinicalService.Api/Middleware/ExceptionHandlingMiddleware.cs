using System.Net;
using System.Text.Json;
using FluentValidation;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.Contracts;
using His.Hope.Infrastructure.Contracts;
using His.Hope.SharedKernel.Domain.Exceptions;
using NotFoundException = His.Hope.SharedKernel.Domain.Exceptions.NotFoundException;

namespace His.Hope.ClinicalService.Api.Middleware;

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
        HttpStatusCode statusCode;
        string title;
        string? detail;
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Bad Request";
                detail = "Validation failed.";
                errors = validationEx.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
                break;

            case DomainException domainEx:
                statusCode = HttpStatusCode.UnprocessableEntity;
                title = "Unprocessable Entity";
                detail = domainEx.Message;
                break;

            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = notFoundEx.Message;
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                title = "Internal Server Error";
                detail = "An unexpected error occurred.";
                break;
        }

        var statusValue = (int)statusCode;
        var correlationId = context.Request.Headers[His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Headers.CorrelationId].FirstOrDefault()
            ?? context.TraceIdentifier;
        var error = new ApiErrorLogEntry(
            ApiErrorCodes.ForStatus(statusValue),
            statusValue,
            exception.Message,
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.TraceIdentifier,
            statusValue >= 500 ? null : detail);
        _logger.Log(statusValue >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception, "HTTP error {@Error}", error);
        await context.WriteContractProblemAsync((int)statusCode, title, detail,
            ApiErrorCodes.ForStatus((int)statusCode), errors);
    }
}
