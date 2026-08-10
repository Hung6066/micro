using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Api.Configuration;

public sealed record OidcSecurityOptions(
    SecurityKey? SigningKey,
    SecurityKey? EncryptionKey,
    IReadOnlyList<SecurityKey> EncryptionKeys,
    bool UseEphemeralDevelopmentKeys,
    bool AllowInsecureHttp);

public static class OidcSecurityConfiguration
{
    public static OidcSecurityOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var signingKey = TryLoadRsaSigningKey(configuration);
        var encryptionKeys = LoadRsaEncryptionKeys(configuration);
        var encryptionKey = encryptionKeys.FirstOrDefault();
        var allowInsecureHttp = configuration.GetValue<bool?>("OpenIddict:AllowInsecureHttp")
            ?? environment.IsDevelopment();
        var localHttpMode = configuration.GetValue("HisHope:LocalHttpMode", false);

        if (environment.IsProduction() && (signingKey is null || encryptionKey is null))
        {
            if (signingKey is null)
            {
                throw new InvalidOperationException(
                    "OpenIddict production signing requires a persistent RSA key. " +
                    "Set OpenIddict:Signing:PrivateKeyPath or Jwt:RsaPrivateKeyPath.");
            }

            throw new InvalidOperationException(
                "OpenIddict production encryption requires a persistent RSA key. " +
                "Set OpenIddict:Encryption:PrivateKeyPath or Jwt:RsaEncryptionPrivateKeyPath.");
        }

        if (environment.IsProduction() && allowInsecureHttp && !localHttpMode)
        {
            throw new InvalidOperationException(
                "OpenIddict:AllowInsecureHttp cannot be true in Production.");
        }

        return new OidcSecurityOptions(
            signingKey,
            encryptionKey,
            encryptionKeys,
            UseEphemeralDevelopmentKeys: signingKey is null && encryptionKey is null,
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

    private static IReadOnlyList<SecurityKey> LoadRsaEncryptionKeys(IConfiguration configuration)
    {
        var configuredPaths = new List<string?>
        {
            configuration["OpenIddict:Encryption:PrivateKeyPath"],
            configuration["Jwt:RsaEncryptionPrivateKeyPath"],
            configuration["OpenIddict:Encryption:PreviousPrivateKeyPath"]
        };
        configuredPaths.AddRange(configuration.GetSection("OpenIddict:Encryption:PrivateKeyPaths").GetChildren().Select(section => section.Value));

        var keys = new List<SecurityKey>();
        foreach (var privateKeyPath in configuredPaths.Select(path => FirstNonPlaceholder(path)).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(privateKeyPath))
                continue;

            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(privateKeyPath));

            keys.Add(new RsaSecurityKey(rsa)
            {
                KeyId = configuration["OpenIddict:Encryption:KeyId"]
                    ?? "oidc-encryption"
            });
        }

        return keys;
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
