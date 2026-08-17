using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SecretAndTokenPrimitiveTests
{
    [Fact]
    public void Aes_mfa_secret_round_trips_without_exposing_plaintext()
    {
        var provider = CreateProvider();
        var encryptor = new AesMfaSecretEncryptor(provider);
        const string secret = "JBSWY3DPEHPK3PXP";

        var ciphertext = encryptor.Encrypt(secret);

        ciphertext.Should().NotBe(secret);
        encryptor.Decrypt(ciphertext).Should().Be(secret);
        encryptor.Encrypt(secret).Should().NotBe(ciphertext);
    }

    [Fact]
    public void Aes_mfa_secret_rejects_tampered_ciphertext()
    {
        var provider = CreateProvider();
        var encryptor = new AesMfaSecretEncryptor(provider);
        var ciphertext = encryptor.Encrypt("mfa-secret");
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0x01;

        var act = () => encryptor.Decrypt(Convert.ToBase64String(bytes));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_not_the_raw_token()
    {
        const string token = "refresh-token-value";

        var hash = RefreshTokenRecord.ComputeHash(token);

        hash.Should().HaveLength(64);
        hash.Should().Be(RefreshTokenRecord.ComputeHash(token));
        hash.Should().NotContain(token);
        hash.Should().NotBe(RefreshTokenRecord.ComputeHash(token + "-changed"));
    }

    [Fact]
    public void Refresh_token_record_defaults_are_safe_for_new_records()
    {
        var record = new RefreshTokenRecord();

        record.Id.Should().NotBeNullOrWhiteSpace();
        record.IsUsed.Should().BeFalse();
        record.IsRevoked.Should().BeFalse();
        record.UserId.Should().BeEmpty();
        record.TokenHash.Should().BeEmpty();
        record.FamilyId.Should().BeEmpty();
    }

    private static IDataProtectionProvider CreateProvider()
    {
        var keyDirectory = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            "his-hope-identity-test-keys",
            Guid.NewGuid().ToString("N")));

        return DataProtectionProvider.Create(
            keyDirectory,
            configuration => configuration.SetApplicationName("identity-service-tests"));
    }
}
