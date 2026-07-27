using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Validation;

public static class ValidationExtensions
{
    public static IServiceCollection AddHisHopeValidation(
        this IServiceCollection services,
        Assembly validatorAssembly)
    {
        ArgumentNullException.ThrowIfNull(validatorAssembly);
        services.AddValidatorsFromAssembly(validatorAssembly);
        return services;
    }

    public static IApplicationBuilder UseHisHopeValidationErrors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ValidationExceptionMiddleware>();
    }
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(result => result.Errors).Where(error => error is not null).ToArray();
        if (failures.Length > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

public sealed class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var errors = exception.Errors
                .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "request" : error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://errors.his-hope.local/validation",
                title = "One or more validation errors occurred.",
                status = StatusCodes.Status400BadRequest,
                errorCode = "VALIDATION_ERROR",
                correlationId = context.Items["His.Hope.AspNetCore.CorrelationId"]?.ToString()
                    ?? context.TraceIdentifier,
                errors
            });
            context.Response.ContentType = "application/problem+json";
        }
    }
}
