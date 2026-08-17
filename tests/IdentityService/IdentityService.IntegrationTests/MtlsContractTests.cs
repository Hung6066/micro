using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.IdentityService.Api.Endpoints;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class MtlsContractTests
{
    [Fact]
    public void CertificateThumbprintsAreNormalizedForBinding()
    {
        var method = typeof(MtlsEndpoints).GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal("AABBCC", method!.Invoke(null, [" aa bb cc "]));
        Assert.Equal(string.Empty, method.Invoke(null, [null]));
    }

    [Fact]
    public void ServerAuthenticationCertificateIsRejectedForClientLogin()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=server-only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        var method = typeof(MtlsEndpoints).GetMethod("IsTrustedClientCertificate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var configuration = new ConfigurationBuilder().Build();

        Assert.False((bool)method!.Invoke(null, [certificate, configuration])!);
    }

    [Fact]
    public void ExpiredClientCertificateIsRejectedBeforeTrustEvaluation()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=expired-client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.2") }, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5));
        var method = typeof(MtlsEndpoints).GetMethod("IsTrustedClientCertificate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.False((bool)method!.Invoke(null, [certificate, new ConfigurationBuilder().Build()])!);
    }
}
