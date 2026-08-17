using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens;
using FluentAssertions;
using His.Hope.IdentityService.Api.Services;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class MetadataCertificateValidatorTests
{
    [Fact]
    public void Validate_accepts_a_trusted_certificate_inside_validity_window()
    {
        using var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var validator = new MetadataCertificateValidator([certificate]);

        var act = () => validator.Validate(certificate);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_a_certificate_not_pinned_in_metadata()
    {
        using var pinned = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var presented = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var validator = new MetadataCertificateValidator([pinned]);

        var act = () => validator.Validate(presented);

        act.Should().Throw<SecurityTokenValidationException>().Which.Message.Should().Contain("not trusted");
    }

    [Fact]
    public void Validate_rejects_a_trusted_certificate_outside_validity_window()
    {
        using var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
        var validator = new MetadataCertificateValidator([certificate]);

        var act = () => validator.Validate(certificate);

        act.Should().Throw<SecurityTokenValidationException>().Which.Message.Should().Contain("validity");
    }

    [Fact]
    public void Validate_rejects_when_metadata_has_no_pinned_certificates()
    {
        using var presented = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var validator = new MetadataCertificateValidator([]);

        var act = () => validator.Validate(presented);

        act.Should().Throw<SecurityTokenValidationException>()
            .Which.Message.Should().Contain("not trusted");
    }

    [Fact]
    public void Validate_accepts_a_reloaded_certificate_with_the_same_thumbprint()
    {
        using var pinned = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var presented = new X509Certificate2(pinned.Export(X509ContentType.Cert));
        var validator = new MetadataCertificateValidator([pinned]);

        var act = () => validator.Validate(presented);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_a_trusted_certificate_before_its_validity_window()
    {
        using var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2));
        var validator = new MetadataCertificateValidator([certificate]);

        var act = () => validator.Validate(certificate);

        act.Should().Throw<SecurityTokenValidationException>().Which.Message.Should().Contain("validity");
    }

    private static X509Certificate2 CreateCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=identity-test-metadata",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
