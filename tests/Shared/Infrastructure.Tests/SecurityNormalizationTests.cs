using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Net.Http;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class SecurityNormalizationTests
{
    [Fact]
    public void OidcConfiguration_UsesConfiguredAuthorityAndFactoryBackchannel()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "http://identity.test:5001",
                ["Jwt:Issuer"] = "His.Hope.IdentityService",
                ["Jwt:Audience"] = "His.Hope",
                ["Jwt:AllowHttp"] = "true"
            })
            .Build();

        services.AddHisHopeJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("http://identity.test:5001", options.Authority);
        Assert.Equal(
            "http://identity.test:5001/.well-known/openid-configuration",
            options.MetadataAddress);
        Assert.NotNull(options.Backchannel);
    }

    [Fact]
    public async Task OidcConfiguration_WhenAuthorityFails_PropagatesAsyncFailure()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "http://identity.invalid:5001",
                ["Jwt:AllowHttp"] = "true"
            })
            .Build();

        services.AddHisHopeJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.MetadataAddress!,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(options.Backchannel) { RequireHttps = false });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            options.ConfigurationManager.GetConfigurationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OidcConfiguration_WhenJwksFails_PropagatesAsyncFailure()
    {
        using var client = new HttpClient(new JwksFailureHandler());
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            "http://identity.test/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(client) { RequireHttps = false });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            manager.GetConfigurationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesIncomingIdAndWritesResponseHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "corr-123";
        string? observedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(async ctx =>
        {
            observedCorrelationId = CorrelationContext.CurrentId;
            await ctx.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("corr-123", observedCorrelationId);
    }

    [Fact]
    public async Task SecurityHeadersMiddleware_AddsHeadersAndHttpsHsts()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Contains("max-age=31536000", context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
    }

    private sealed class JwksFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"issuer\":\"http://identity.test\",\"jwks_uri\":\"http://identity.test/.well-known/jwks\"}")
                });
            }

            throw new HttpRequestException("JWKS endpoint unavailable");
        }
    }
}
