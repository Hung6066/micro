using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
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
        if (!string.IsNullOrWhiteSpace(caPath))
        {
            // A configured production CA is a security contract, not an optional
            // hint. Do not silently downgrade to the platform trust store or to
            // plaintext when the CA mount/connection scheme is wrong.
            if (!File.Exists(caPath))
                throw new InvalidOperationException($"Redis TLS CA file '{caPath}' is missing.");
            if (!options.Ssl)
                throw new InvalidOperationException("Redis TLS CA is configured but the Redis connection is not using TLS.");

            var caCertificates = new X509Certificate2Collection();
            caCertificates.ImportFromPem(File.ReadAllText(caPath));
            if (caCertificates.Count == 0)
                throw new InvalidOperationException($"Redis TLS CA file '{caPath}' contains no certificates.");

            options.CertificateValidation += (_, certificate, presentedChain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None)
                    return true;
                if (certificate is null)
                    return false;

                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                var roots = caCertificates
                    .Cast<X509Certificate2>()
                    .Where(IsSelfIssued)
                    .ToArray();
                if (roots.Length > 0)
                {
                    chain.ChainPolicy.CustomTrustStore.AddRange(roots);
                    foreach (var caCertificate in caCertificates.Cast<X509Certificate2>().Except(roots))
                        chain.ChainPolicy.ExtraStore.Add(caCertificate);
                }
                else
                {
                    // Preserve support for an explicitly configured CA bundle that
                    // contains only an intermediate trust anchor.
                    chain.ChainPolicy.CustomTrustStore.AddRange(caCertificates);
                }

                if (presentedChain is not null)
                {
                    for (var index = 1; index < presentedChain.ChainElements.Count; index++)
                        chain.ChainPolicy.ExtraStore.Add(presentedChain.ChainElements[index].Certificate);
                }

                using var serverCertificate = new X509Certificate2(certificate);
                return chain.Build(serverCertificate);
            };
        }

        var clientCertificatePem = configuration["Redis:TlsClientCertificatePem"];
        var clientPrivateKeyPem = configuration["Redis:TlsClientPrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(clientCertificatePem) || !string.IsNullOrWhiteSpace(clientPrivateKeyPem))
        {
            if (!options.Ssl)
                throw new InvalidOperationException("Redis client certificate is configured but the Redis connection is not using TLS.");
            if (string.IsNullOrWhiteSpace(clientCertificatePem) || string.IsNullOrWhiteSpace(clientPrivateKeyPem))
                throw new InvalidOperationException("Both Redis client certificate and private key must be configured.");

            var clientCertificate = X509Certificate2.CreateFromPem(clientCertificatePem, clientPrivateKeyPem);
            options.SslClientAuthenticationOptions = _ => new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { clientCertificate }
            };
        }

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
            if (credentials.Length == 2 && !string.IsNullOrWhiteSpace(credentials[0]))
                parts.Add($"user={credentials[0]}");
            if (credentials.Length == 2 && !string.IsNullOrWhiteSpace(credentials[1]))
                parts.Add($"password={credentials[1]}");
        }

        var database = redisUri.AbsolutePath.Trim('/');
        if (int.TryParse(database, out var databaseIndex))
            parts.Add($"defaultDatabase={databaseIndex}");

        if (redisUri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
            parts.Add("ssl=True");

        var query = redisUri.Query.TrimStart('?');
        foreach (var item in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = Uri.UnescapeDataString(item[..separator]);
            var value = Uri.UnescapeDataString(item[(separator + 1)..]);
            if (key.Equals("serviceName", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                parts.Add($"serviceName={value}");
        }

        return string.Join(',', parts);
    }

    public static ConnectionMultiplexer Connect(string connectionString, IConfiguration configuration) =>
        ConnectionMultiplexer.Connect(CreateOptions(connectionString, configuration));

    private static bool IsSelfIssued(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);
}
