using FluentAssertions;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using System.Security.Cryptography;
using His.Hope.Contracts;

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
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task Problem_writer_does_not_expose_internal_details_for_server_errors()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await context.WriteHisHopeProblemAsync(500, "Internal error", "database password");

        context.Response.Body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("internal_error");
        var detail = document.RootElement.TryGetProperty("detail", out var detailProperty)
            ? detailProperty.ValueKind
            : System.Text.Json.JsonValueKind.Null;
        detail.Should().Be(System.Text.Json.JsonValueKind.Null);
        document.RootElement.GetProperty("title").GetString().Should().Be("The request could not be completed.");
    }

    [Fact]
    public async Task Problem_details_callback_logs_business_detail_and_request_metadata()
    {
        var provider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddHisHopeProblemDetails();
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Request.Method = "POST";
        context.Request.Path = "/patients";
        context.Request.Headers["X-Correlation-Id"] = "corr-123";
        context.Response.StatusCode = 422;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 422,
            Detail = "Patient is already registered."
        };

        await serviceProvider
            .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem
            });

        var error = provider.LastError.Should().BeOfType<ApiErrorLogEntry>().Subject;
        error.ErrorCode.Should().Be(ApiErrorCodes.UnprocessableEntity);
        error.StatusCode.Should().Be(422);
        error.Detail.Should().Be("Patient is already registered.");
        error.Method.Should().Be("POST");
        error.Path.Should().Be("/patients");
        error.CorrelationId.Should().Be("corr-123");
        error.TraceId.Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public async Task Problem_details_callback_logs_validation_errors()
    {
        var provider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddHisHopeProblemDetails();
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["email"] = ["Email is required."]
        })
        {
            Status = 400
        };

        await serviceProvider
            .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem
            });

        var error = provider.LastError.Should().BeOfType<ApiErrorLogEntry>().Subject;
        error.Errors.Should().ContainKey("email");
        error.Errors!["email"].Should().ContainSingle().Which.Should().Be("Email is required.");
    }

    [Fact]
    public async Task Problem_details_callback_redacts_server_detail_but_keeps_it_out_of_response_contract()
    {
        var provider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(provider));
        services.AddHisHopeProblemDetails();
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 500,
            Detail = "database password"
        };

        await serviceProvider
            .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem
            });

        problem.Detail.Should().Be("database password");
        var error = provider.LastError.Should().BeOfType<ApiErrorLogEntry>().Subject;
        error.Detail.Should().BeNull();
        error.ErrorCode.Should().Be(ApiErrorCodes.Internal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _states = [];

        public object? LastError => _states
            .SelectMany(state => state.Values)
            .OfType<ApiErrorLogEntry>()
            .LastOrDefault();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                    owner._states.Add(values.ToDictionary(pair => pair.Key, pair => pair.Value));
            }
        }
    }
}
