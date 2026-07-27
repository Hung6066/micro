using FluentAssertions;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.AspNetCore.Tests;

public sealed class AspNetCoreExtensionsTests
{
    [Fact]
    public void Jwt_registration_uses_symmetric_validation_when_key_is_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "a-development-key-with-enough-length",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddHisHopeJwtAuthentication(configuration);

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(Microsoft.AspNetCore.Authorization.IAuthorizationService));
    }

    [Fact]
    public async Task Problem_writer_includes_correlation_and_error_code()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/patients";
        context.Request.Headers["X-Correlation-Id"] = "corr-123";
        context.Response.Body = new MemoryStream();

        await context.WriteHisHopeProblemAsync(404, "Not Found");

        context.Response.StatusCode.Should().Be(404);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("correlationId").GetString().Should().Be("corr-123");
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("not-found");
    }
}
