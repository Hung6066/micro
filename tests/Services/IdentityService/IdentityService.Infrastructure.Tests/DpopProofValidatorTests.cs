using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DpopProofValidatorTests
{
    [Fact]
    public void Validate_rejects_missing_or_non_compact_proof()
    {
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);
        var missing = CreateRequest(string.Empty, "GET", "/patients/1");
        var malformed = CreateRequest("not-a-jwt", "GET", "/patients/1");

        validator.Invoking(value => value.Validate(missing)).Should()
            .Throw<DpopValidationException>().WithMessage("*required*");
        validator.Invoking(value => value.Validate(malformed)).Should()
            .Throw<DpopValidationException>().WithMessage("*compact JWT*");
    }

    [Fact]
    public void Validate_rejects_invalid_header_and_payload_encoding()
    {
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        validator.Invoking(value => value.Validate(CreateRequest("%%%.bad.sig", "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*header*");
        var validHeader = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes("{\"typ\":\"dpop+jwt\",\"alg\":\"ES256\"}"));
        validator.Invoking(value => value.Validate(CreateRequest($"{validHeader}.%%%.sig", "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*payload*");
    }

    [Fact]
    public void Validate_rejects_wrong_type_algorithm_key_and_uri()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);

        var wrongType = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"), typ: "jwt");
        validator.Invoking(value => value.Validate(CreateRequest(wrongType, "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*typ*");

        var wrongAlgorithm = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"), alg: "RS256");
        validator.Invoking(value => value.Validate(CreateRequest(wrongAlgorithm, "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*ES256*");

        var valid = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"));
        validator.Invoking(value => value.Validate(CreateRequest(valid, "GET", "/different")))
            .Should().Throw<DpopValidationException>().WithMessage("*htu*");
    }

    [Fact]
    public void Validate_rejects_proof_outside_clock_window_and_oversized_jti()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = new DpopProofValidator(new InMemoryDpopReplayCache(), TimeProvider.System);
        var stale = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", Guid.NewGuid().ToString("N"),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds());
        validator.Invoking(value => value.Validate(CreateRequest(stale, "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*clock window*");

        var oversizedJti = new string('x', 201);
        var oversized = CreateProof(key, "GET", "https://api.his-hope.test/patients/1", oversizedJti);
        validator.Invoking(value => value.Validate(CreateRequest(oversized, "GET", "/patients/1")))
            .Should().Throw<DpopValidationException>().WithMessage("*jti*");
    }

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
        var proof = CreateProof(key, "POST", $"http://10.0.2.2:5000{IdentityApiRoutes.OidcToken}", Guid.NewGuid().ToString("N"));
        var request = CreateRequest(proof, "POST", IdentityApiRoutes.OidcToken);
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

    private static string CreateProof(ECDsa key, string method, string uri, string jti,
        string typ = "dpop+jwt", string alg = SecurityAlgorithms.EcdsaSha256, long? issuedAt = null)
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
        header["typ"] = typ;
        header["alg"] = alg;
        header["jwk"] = jwk;
        var payload = new JwtPayload
        {
            ["htm"] = method,
            ["htu"] = uri,
            ["iat"] = issuedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["jti"] = jti
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }
}
