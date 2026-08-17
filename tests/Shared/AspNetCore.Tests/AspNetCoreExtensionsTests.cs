using FluentAssertions;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using System.Security.Cryptography;

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
    public void Oidc_registration_loads_rsa_decryption_key_for_jwe_tokens()
    {
        using var rsa = RSA.Create(2048);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "https://identity.test",
                ["Jwt:RsaEncryptionPrivateKey"] = rsa.ExportRSAPrivateKeyPem(),
                ["Jwt:AllowHttp"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddHisHopeJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var decryptionKey = options.TokenValidationParameters.TokenDecryptionKey;

        decryptionKey.Should().BeOfType<RsaSecurityKey>();
        ((RsaSecurityKey)decryptionKey!).Rsa.KeySize.Should().BeGreaterThanOrEqualTo(2048);
    }

    [Fact]
    public void Placeholder_hmac_key_uses_oidc_validation_instead_of_legacy_hmac()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "${JWT_SIGNING_KEY}",
                ["Jwt:Authority"] = "https://identity.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddHisHopeJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.TokenValidationParameters.ValidAlgorithms.Should().Contain(SecurityAlgorithms.RsaSha256);
    }

    [Fact]
    public void Oidc_registration_rejects_unconfigured_audiences_by_enabling_audience_validation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "https://identity.test",
                ["Jwt:Issuer"] = "https://identity.test",
                ["Jwt:Audience"] = "service-a",
                ["Jwt:AllowHttp"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddHisHopeJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        options.TokenValidationParameters.ValidAudiences.Should().Contain("service-a");
        options.TokenValidationParameters.ValidAudiences.Should().Contain("his-hope-services");
        options.TokenValidationParameters.ValidAudiences.Should().NotContain("other-service");
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
