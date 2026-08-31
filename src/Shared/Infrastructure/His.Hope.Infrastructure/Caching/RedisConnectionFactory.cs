using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Caching;

public static class RedisConnectionFactory
{
    public static ConfigurationOptions CreateOptions(string connectionString, IConfiguration configuration)
    {
        var options = ConfigurationOptions.Parse(NormalizeConnectionString(connectionString));
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 1;
        options.ConnectTimeout = Math.Min(options.ConnectTimeout, 5000);
        options.SyncTimeout = Math.Min(options.SyncTimeout, 5000);

        var caPath = configuration["Redis:TlsCaFile"];
        if (string.IsNullOrWhiteSpace(caPath))
            return options;

        // A configured production CA is a security contract, not an optional
        // hint. Do not silently downgrade to the platform trust store or to
        // plaintext when the CA mount/connection scheme is wrong.
        if (!File.Exists(caPath))
            throw new InvalidOperationException($"Redis TLS CA file '{caPath}' is missing.");
        if (!options.Ssl)
            throw new InvalidOperationException("Redis TLS CA is configured but the Redis connection is not using TLS.");

        var caCertificate = new X509Certificate2(caPath);
        options.CertificateValidation += (_, certificate, _, errors) =>
        {
            if (errors == System.Net.Security.SslPolicyErrors.None)
                return true;
            if (certificate is null)
                return false;

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(caCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(new X509Certificate2(certificate));
        };

        return options;
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var redisUri) ||
            string.IsNullOrWhiteSpace(redisUri.Host) ||
            (!redisUri.Scheme.Equals("redis", StringComparison.OrdinalIgnoreCase) &&
             !redisUri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)))
        {
            return connectionString;
        }

        var parts = new List<string>
        {
            redisUri.IsDefaultPort ? redisUri.Host : $"{redisUri.Host}:{redisUri.Port}"
        };

        if (!string.IsNullOrWhiteSpace(redisUri.UserInfo))
        {
            var credentials = Uri.UnescapeDataString(redisUri.UserInfo).Split(':', 2);
            if (credentials.Length == 2 && !string.IsNullOrWhiteSpace(credentials[1]))
                parts.Add($"password={credentials[1]}");
        }

        var database = redisUri.AbsolutePath.Trim('/');
        if (int.TryParse(database, out var databaseIndex))
            parts.Add($"defaultDatabase={databaseIndex}");

        if (redisUri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
            parts.Add("ssl=True");

        return string.Join(',', parts);
    }

    public static ConnectionMultiplexer Connect(string connectionString, IConfiguration configuration) =>
        ConnectionMultiplexer.Connect(CreateOptions(connectionString, configuration));
}
