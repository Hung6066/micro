using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using His.Hope.Configuration;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace His.Hope.Infrastructure.Messaging;

/// <summary>
/// Applies the shared broker settings that every hand-built <see cref="ConnectionFactory"/> must honour:
/// the virtual host and, when <c>EventBus:UseSsl</c> is set, TLS with an optional client certificate
/// (<c>EventBus:ClientCertificatePath</c> + <c>EventBus:ClientPrivateKeyPath</c>) and a private CA
/// (<c>EventBus:CaCertificatePath</c>).
/// </summary>
public static class RabbitMqPlatformTls
{
    public static ConnectionFactory ApplyPlatformTls(this ConnectionFactory factory, IConfiguration configuration)
    {
        var virtualHost = configuration[HisHopeConfigurationKeys.EventBus.VirtualHost];
        if (!string.IsNullOrWhiteSpace(virtualHost)) factory.VirtualHost = virtualHost;

        if (!configuration.GetValue(HisHopeConfigurationKeys.EventBus.UseSsl, false)) return factory;

        factory.Ssl.Enabled = true;
        factory.Ssl.ServerName = configuration[HisHopeConfigurationKeys.EventBus.SslServerName] is { Length: > 0 } serverName
            ? serverName
            : factory.HostName;

        var certificatePath = configuration[HisHopeConfigurationKeys.EventBus.ClientCertificatePath];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var keyPath = configuration[HisHopeConfigurationKeys.EventBus.ClientPrivateKeyPath];
            var password = configuration[HisHopeConfigurationKeys.EventBus.ClientCertificatePassword];
            var certificate = !string.IsNullOrWhiteSpace(keyPath)
                ? X509Certificate2.CreateFromPemFile(certificatePath, keyPath)
                : string.IsNullOrEmpty(password)
                    ? new X509Certificate2(certificatePath)
                    : new X509Certificate2(certificatePath, password);
            factory.Ssl.Certs = new X509Certificate2Collection { certificate };
        }

        var caPath = configuration[HisHopeConfigurationKeys.EventBus.CaCertificatePath];
        if (!string.IsNullOrWhiteSpace(caPath))
        {
            var roots = new X509Certificate2Collection();
            roots.ImportFromPemFile(caPath);
            factory.Ssl.CertificateValidationCallback = (_, certificate, _, errors) =>
                ValidateAgainstPrivateCa(roots, certificate, errors);
        }

        return factory;
    }

    private static bool ValidateAgainstPrivateCa(
        X509Certificate2Collection roots,
        X509Certificate? certificate,
        SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;
        if (certificate is null) return false;
        // Host name mismatches and missing certificates are never acceptable; only the chain is re-checked.
        if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None) return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(roots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(new X509Certificate2(certificate));
    }
}
