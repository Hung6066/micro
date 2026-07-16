using System.Net;
using System.Text.Json;
using FluentValidation;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Api.Middleware;

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
        context.Response.ContentType = "application/json";
        HttpStatusCode statusCode;
        object response;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = HttpStatusCode.BadRequest;
                response = new { error = "Validation failed", details = validationEx.Errors };
                break;

            case DomainException domainEx:
                statusCode = HttpStatusCode.UnprocessableEntity;
                response = new { error = domainEx.Message };
                break;

            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                response = new { error = notFoundEx.Message };
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                response = new { error = exception.Message, type = exception.GetType().Name, stackTrace = exception.StackTrace?.Substring(0, 500) };
                break;
        }

        _logger.LogError(exception, "Request failed with {StatusCode}", statusCode);

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
