using System.Net;
using System.Text.Json;
using FluentValidation;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.PatientService.Api.Middleware;

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

        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new { error = "Validation failed", details = validationEx.Errors }),

            DomainException domainEx => (
                HttpStatusCode.UnprocessableEntity,
                new { error = domainEx.Message }),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new { error = notFoundEx.Message }),

            _ => (
                HttpStatusCode.InternalServerError,
                new { error = "An unexpected error occurred." })
        };

        _logger.LogError(exception, "Request failed with {StatusCode}", statusCode);

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
