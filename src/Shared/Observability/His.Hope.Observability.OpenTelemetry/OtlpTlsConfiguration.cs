using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace His.Hope.Observability.OpenTelemetry;

internal static class OtlpTlsConfiguration
{
    internal static Func<HttpClient>? ReadHttpClientFactory(IConfiguration configuration, Uri? endpoint)
    {
        if (endpoint is null)
            return null;

        var key = new[] { "OTEL_EXPORTER_OTLP_CERTIFICATE", "OpenTelemetry:CaCertificatePath", "Otlp:CaCertificatePath" }
            .FirstOrDefault(candidate => configuration[candidate] is not null);
        if (key is null)
            return null;
        if (endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{key} requires an HTTPS OTLP endpoint.");

        var certificates = new X509Certificate2Collection();
        try
        {
            certificates.ImportFromPemFile(configuration[key]!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException($"{key} must name a readable PEM CA certificate bundle.", exception);
        }

        if (certificates.Count == 0 ||
            !certificates.Cast<X509Certificate2>().Any(IsRoot))
            throw new InvalidOperationException($"{key} must contain a root CA certificate.");

        return () => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
                ValidateServerCertificate(certificates, certificate, chain, errors)
        });
    }

    internal static bool ValidateServerCertificate(
        X509Certificate2Collection certificates,
        X509Certificate2? certificate,
        X509Chain? presentedChain,
        SslPolicyErrors errors)
    {
        // As with the shared Redis/RabbitMQ trust policy, only chain errors
        // may be re-evaluated. Hostname mismatches are never accepted.
        if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new("1.3.6.1.5.5.7.3.1"));
        foreach (var ca in certificates)
        {
            if (IsRoot(ca))
                chain.ChainPolicy.CustomTrustStore.Add(ca);
            else
                chain.ChainPolicy.ExtraStore.Add(ca);
        }
        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements)
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        // A configured CA is authoritative even if the OS trusts another chain.
        return chain.Build(certificate);
    }

    private static bool IsRoot(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData) &&
        certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(extension => extension.CertificateAuthority);
}
