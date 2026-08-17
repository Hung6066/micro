using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using His.Hope.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

 [Collection("shared-redis")]
public sealed class DpopResourceProofValidatorTests(RedisTestFixture fixture)
{
    private readonly IConnectionMultiplexer _connection = fixture.Connection;

    [Fact]
    public void Valid_proof_is_accepted_once_and_replay_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = CreateJwk(key);
        var accessToken = "access-token";
        var request = new DefaultHttpContext();
        request.Request.Scheme = "https";
        request.Request.Host = new HostString("api.test");
        request.Request.Method = HttpMethods.Get;
        request.Request.Path = "/api/resource";
        request.Request.QueryString = new QueryString("?page=1");
        var proof = CreateProof(key, jwk, request, accessToken, "jti-1");
        request.Request.Headers["DPoP"] = proof;

        var validator = new DpopResourceProofValidator(_connection);
        var thumbprint = Thumbprint(jwk);

        Assert.True(validator.Validate(request.Request, thumbprint, accessToken));
        Assert.False(validator.Validate(request.Request, thumbprint, accessToken));
    }

    [Fact]
    public void Wrong_access_token_hash_is_rejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = CreateJwk(key);
        var request = new DefaultHttpContext();
        request.Request.Scheme = "https";
        request.Request.Host = new HostString("api.test");
        request.Request.Method = HttpMethods.Post;
        request.Request.Path = "/api/resource";
        request.Request.Headers["DPoP"] = CreateProof(key, jwk, request, "different-token", "jti-2");

        var validator = new DpopResourceProofValidator(_connection);

        Assert.False(validator.Validate(request.Request, Thumbprint(jwk), "presented-token"));
    }

    private static string CreateProof(ECDsa key, JsonWebKey jwk, HttpContext context, string accessToken, string jti)
    {
        var requestUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}";
        var header = new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256));
        header["typ"] = "dpop+jwt";
        header["jwk"] = new Dictionary<string, object>
        {
            ["kty"] = jwk.Kty!,
            ["crv"] = jwk.Crv!,
            ["x"] = jwk.X!,
            ["y"] = jwk.Y!
        };
        var payload = new JwtPayload
        {
            ["htm"] = context.Request.Method,
            ["htu"] = requestUri,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["jti"] = jti,
            ["ath"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)))
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private static JsonWebKey CreateJwk(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        return new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Crv = "P-256",
            X = Base64UrlEncoder.Encode(parameters.Q.X!),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y!)
        };
    }

    private static string Thumbprint(JsonWebKey jwk)
    {
        var canonical = $"{{\"crv\":\"{jwk.Crv}\",\"kty\":\"{jwk.Kty}\",\"x\":\"{jwk.X}\",\"y\":\"{jwk.Y}\"}}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
