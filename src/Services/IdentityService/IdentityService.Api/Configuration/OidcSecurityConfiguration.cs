using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Api.Configuration;

public sealed record OidcSecurityOptions(
    SecurityKey? SigningKey,
    bool UseEphemeralDevelopmentKeys,
    bool AllowInsecureHttp);

public static class OidcSecurityConfiguration
{
    public static OidcSecurityOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var signingKey = TryLoadRsaSigningKey(configuration);
        var allowInsecureHttp = configuration.GetValue<bool?>("OpenIddict:AllowInsecureHttp")
            ?? environment.IsDevelopment();

        if (environment.IsProduction() && signingKey is null)
        {
            throw new InvalidOperationException(
                "OpenIddict production signing requires a persistent RSA key. " +
                "Set OpenIddict:Signing:PrivateKeyPath or Jwt:RsaPrivateKeyPath.");
        }

        if (environment.IsProduction() && allowInsecureHttp)
        {
            throw new InvalidOperationException(
                "OpenIddict:AllowInsecureHttp cannot be true in Production.");
        }

        return new OidcSecurityOptions(
            signingKey,
            UseEphemeralDevelopmentKeys: signingKey is null,
            AllowInsecureHttp: allowInsecureHttp);
    }

    private static SecurityKey? TryLoadRsaSigningKey(IConfiguration configuration)
    {
        var privateKeyPath = FirstNonPlaceholder(
            configuration["OpenIddict:Signing:PrivateKeyPath"],
            configuration["Jwt:RsaPrivateKeyPath"]);

        if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
            return null;

        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));

        return new RsaSecurityKey(rsa)
        {
            KeyId = configuration["OpenIddict:Signing:KeyId"]
                ?? configuration["Vault:Transit:KeyName"]
                ?? "jwt-signing"
        };
    }

    private static string? FirstNonPlaceholder(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'))
                continue;

            return value;
        }

        return null;
    }
}
