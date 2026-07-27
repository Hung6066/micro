using FluentAssertions;
using FluentValidation;
using His.Hope.Validation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace His.Hope.Validation.Tests;

public sealed class ValidationContractTests
{
    [Fact]
    public async Task Behavior_rejects_invalid_request_before_handler()
    {
        var behavior = new ValidationBehavior<CreateRequest, Unit>(new[] { new CreateRequestValidator() });
        var handlerCalled = false;

        var action = () => behavior.Handle(
            new CreateRequest(string.Empty),
            () =>
            {
                handlerCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<ValidationException>();
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Behavior_allows_valid_request()
    {
        var behavior = new ValidationBehavior<CreateRequest, Unit>(new[] { new CreateRequestValidator() });
        var result = await behavior.Handle(
            new CreateRequest("valid"),
            () => Task.FromResult(Unit.Value),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Middleware_writes_stable_problem_details_shape()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = new ValidationExceptionMiddleware(_ => throw new ValidationException(
            new[] { new FluentValidation.Results.ValidationFailure("email", "Email is required.") }));

        await middleware.Invoke(context);
        body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(body);
        var root = document.RootElement;

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        root.GetProperty("errorCode").GetString().Should().Be("VALIDATION_ERROR");
        root.GetProperty("errors").GetProperty("email")[0].GetString().Should().Be("Email is required.");
    }

    private sealed record CreateRequest(string Name);

    private sealed class CreateRequestValidator : AbstractValidator<CreateRequest>
    {
        public CreateRequestValidator() => RuleFor(request => request.Name).NotEmpty();
    }
}
