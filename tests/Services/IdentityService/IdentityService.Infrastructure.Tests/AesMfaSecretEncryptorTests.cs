using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class AesMfaSecretEncryptorTests
{
    [Fact]
    public void Encrypt_and_decrypt_round_trip_unicode_secret()
    {
        var encryptor = CreateEncryptor();

        var ciphertext = encryptor.Encrypt("mfa-secret-🔐-ภาษาไทย");

        ciphertext.Should().NotBeNullOrWhiteSpace();
        ciphertext.Should().NotBe("mfa-secret-🔐-ภาษาไทย");
        encryptor.Decrypt(ciphertext).Should().Be("mfa-secret-🔐-ภาษาไทย");
    }

    [Fact]
    public void Encrypt_empty_secret_is_reversible()
    {
        var encryptor = CreateEncryptor();

        var ciphertext = encryptor.Encrypt(string.Empty);

        encryptor.Decrypt(ciphertext).Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("")]
    public void Decrypt_rejects_invalid_ciphertext(string ciphertext)
    {
        var act = () => CreateEncryptor().Decrypt(ciphertext);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Protectors_are_isolated_by_provider_key_ring()
    {
        var first = CreateEncryptor(DataProtectionProvider.Create("identity-test-a"));
        var second = CreateEncryptor(DataProtectionProvider.Create("identity-test-b"));
        var ciphertext = first.Encrypt("secret");

        var act = () => second.Decrypt(ciphertext);

        act.Should().Throw<Exception>();
    }

    private static AesMfaSecretEncryptor CreateEncryptor(IDataProtectionProvider? provider = null) =>
        new(provider ?? DataProtectionProvider.Create("identity-test"));
}
