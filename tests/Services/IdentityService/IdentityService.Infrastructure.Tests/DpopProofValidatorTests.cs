using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DpopProofValidatorTests
{
    [Fact]
    public void Validate_AcceptsValidProofAndReturnsJwkThumbprint()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        var cache = new InMemoryDpopReplayCache();
        var validator = new DpopProofValidator(cache, TimeProvider.System);

        var result = validator.Validate(CreateRequest(proof, "GET", "/patients/1"));

        result.JwkThumbprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_RejectsProofForDifferentHttpMethod()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "POST", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        var act = () => validator.Validate(CreateRequest(proof, "GET", "/patients/1"));

        act.Should().Throw<DpopValidationException>().Which.Message.Should().Contain("htm");
    }

    [Fact]
    public void Validate_RejectsReplayedJti()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        var cache = new InMemoryDpopReplayCache();
        var validator = new DpopProofValidator(cache, TimeProvider.System);

        validator.Validate(CreateRequest(proof, "GET", "/patients/1"));
        var act = () => validator.Validate(CreateRequest(proof, "GET", "/patients/1"));

        act.Should().Throw<DpopValidationException>().Which.Message.Should().Contain("replay");
    }

    [Fact]
    public void Validate_RejectsProofWhenTokenBindingThumbprintDoesNotMatch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        var act = () => validator.Validate(
            CreateRequest(proof, "GET", "/patients/1"),
            expectedJwkThumbprint: "wrong-thumbprint");

        act.Should().Throw<DpopValidationException>().Which.Message.Should().Contain("binding");
    }

    [Fact]
    public void Validate_IgnoresQueryStringInRequestUriBinding()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        var request = CreateRequest(proof, "GET", "/patients/1");
        request.QueryString = new QueryString("?include=summary");

        validator.Validate(request).JwkThumbprint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_UsesForwardedPublicOriginBehindGateway()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = CreateProof(key, "POST", "http://10.0.2.2:5000/connect/token", Guid.NewGuid().ToString("N"));
        var request = CreateRequest(proof, "POST", "/connect/token");
        request.Scheme = "http";
        request.Host = new HostString("identityservice", 5001);
        request.Headers["X-Forwarded-Proto"] = "http";
        request.Headers["X-Forwarded-Host"] = "10.0.2.2:5000";

        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        validator.Validate(request).JwkThumbprint.Should().NotBeNullOrWhiteSpace();
    }

    private static HttpRequest CreateRequest(string proof, string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.his-hope.test");
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers["DPoP"] = proof;
        return context.Request;
    }

    private static string CreateProof(ECDsa key, string method, string uri, string jti)
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
            ["jti"] = jti
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }
}
