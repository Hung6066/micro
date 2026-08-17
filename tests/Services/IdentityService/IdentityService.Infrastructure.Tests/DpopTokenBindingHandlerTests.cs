using FluentAssertions;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DpopTokenBindingHandlerTests
{
    [Fact]
    public async Task Non_required_client_is_left_unchanged()
    {
        var (handler, _) = CreateHandler("mobile");
        var context = CreateContext("browser");

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task Required_client_without_http_context_is_rejected()
    {
        var accessor = new HttpContextAccessor();
        var handler = new DpopTokenBindingHandler(
            new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System),
            accessor,
            Configuration("mobile"));
        var context = CreateContext("mobile");

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidRequest);
        context.ErrorDescription.Should().Contain("HTTP request");
    }

    [Fact]
    public async Task Required_client_with_missing_proof_is_rejected_fail_closed()
    {
        var (handler, accessor) = CreateHandler("mobile");
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        accessor.HttpContext = http;
        var context = CreateContext("mobile");

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidRequest);
        context.ErrorDescription.Should().Contain("DPoP proof is required");
    }

    [Fact]
    public async Task Valid_proof_binds_principal_with_confirmation_claim()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.his-hope.test");
        http.Request.Path = IdentityApiRoutes.OidcToken;
        http.Request.Headers["DPoP"] = CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}");
        accessor.HttpContext = http;
        var identity = new ClaimsIdentity();
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(identity);

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        identity.FindFirst("cnf")!.Value.Should().Contain("jkt");
    }

    [Fact]
    public async Task Required_client_without_token_principal_is_rejected_fail_closed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        var http = CreateHttpContext(CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}"));
        accessor.HttpContext = http;
        var context = CreateContext("mobile");

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidRequest);
        context.ErrorDescription.Should().Contain("token principal");
    }

    [Fact]
    public async Task Existing_matching_binding_is_preserved_and_request_is_accepted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        accessor.HttpContext = CreateHttpContext(
            CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}"));
        var identity = new ClaimsIdentity();
        var thumbprint = CreateJwkThumbprint(key);
        identity.AddClaim(new Claim("cnf", $"{{\"jkt\":\"{thumbprint}\"}}"));
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(identity);

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        identity.FindAll("cnf").Should().ContainSingle();
        identity.FindFirst("cnf")!.Value.Should().Be($"{{\"jkt\":\"{thumbprint}\"}}");
    }

    [Fact]
    public async Task Malformed_existing_binding_is_ignored_and_replaced_with_valid_binding()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        accessor.HttpContext = CreateHttpContext(
            CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}"));
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("cnf", "not-json"));
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(identity);

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        identity.FindAll("cnf").Should().ContainSingle();
        identity.FindFirst("cnf")!.Value.Should().Contain("jkt");
    }

    [Fact]
    public async Task Existing_binding_without_thumbprint_is_ignored_and_replaced()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        accessor.HttpContext = CreateHttpContext(
            CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}"));
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("cnf", "{\"foo\":\"bar\"}"));
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(identity);

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        identity.FindAll("cnf").Should().ContainSingle();
        identity.FindFirst("cnf")!.Value.Should().Contain("jkt");
    }

    [Fact]
    public async Task Malformed_proof_is_rejected_as_invalid_request()
    {
        var (handler, accessor) = CreateHandler("mobile");
        accessor.HttpContext = CreateHttpContext("not-a-compact-jwt");
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(new ClaimsIdentity());

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidRequest);
        context.ErrorDescription.Should().Contain("compact JWT");
    }

    [Fact]
    public async Task Existing_mismatched_binding_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (handler, accessor) = CreateHandler("mobile");
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.his-hope.test");
        http.Request.Path = IdentityApiRoutes.OidcToken;
        http.Request.Headers["DPoP"] = CreateProof(key, "POST", $"https://api.his-hope.test{IdentityApiRoutes.OidcToken}");
        accessor.HttpContext = http;
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("cnf", "{\"jkt\":\"different\"}"));
        var context = CreateContext("mobile");
        context.Principal = new ClaimsPrincipal(identity);

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidGrant);
    }

    private static (DpopTokenBindingHandler Handler, HttpContextAccessor Accessor) CreateHandler(string requiredClient)
    {
        var accessor = new HttpContextAccessor();
        return (new DpopTokenBindingHandler(
                new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System),
                accessor,
                Configuration(requiredClient)), accessor);
    }

    private static DefaultHttpContext CreateHttpContext(string proof)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.his-hope.test");
        http.Request.Path = IdentityApiRoutes.OidcToken;
        http.Request.Headers["DPoP"] = proof;
        return http;
    }

    private static OpenIddictServerEvents.HandleTokenRequestContext CreateContext(string clientId)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = clientId }
        };
        return new OpenIddictServerEvents.HandleTokenRequestContext(transaction);
    }

    private static IConfiguration Configuration(string clientId) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dpop:RequiredClientIds:0"] = clientId
        }).Build();

    private static string CreateProof(ECDsa key, string method, string uri)
    {
        var parameters = key.ExportParameters(false);
        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X!),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y!)
        };
        var header = new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256));
        header["typ"] = "dpop+jwt";
        header["jwk"] = jwk;
        var payload = new JwtPayload
        {
            ["htm"] = method,
            ["htu"] = uri,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N")
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private static string CreateJwkThumbprint(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = "EC",
            Crv = "P-256",
            X = Base64UrlEncoder.Encode(parameters.Q.X!),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y!)
        };
        return DpopProofValidator.CreateJwkThumbprint(jwk);
    }
}
