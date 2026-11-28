using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using His.Hope.IdentityService.Application.Scim;
using His.Hope.IdentityService.Application.Services;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class SecurityServiceTests
{
    [Fact]
    public void Totp_generates_a_valid_secret_and_uri()
    {
        var service = new TotpService();

        var secret = service.GenerateSecret();
        var uri = service.GenerateQrCodeUri(secret, "doctor+test@example.com", "His Hope");

        secret.Should().MatchRegex("^[A-Z2-7]{32}$");
        uri.Should().Be($"otpauth://totp/His%20Hope:doctor%2Btest%40example.com?secret={secret}&issuer=His%20Hope&algorithm=SHA1&digits=6&period=30");
    }

    [Fact]
    public void Totp_rejects_empty_malformed_and_wrong_codes()
    {
        var service = new TotpService();

        service.VerifyCode("", "123456").Should().BeFalse();
        service.VerifyCode("not-base32", "123456").Should().BeFalse();
        service.VerifyCode("JBSWY3DPEHPK3PXP", "000000").Should().BeFalse();
    }

    [Fact]
    public void Totp_accepts_the_current_code_and_allowed_drift_window()
    {
        var service = new TotpService();
        const string secret = "JBSWY3DPEHPK3PXP";
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        var current = GenerateCode(secret, counter);
        var previous = GenerateCode(secret, counter - 1);

        service.VerifyCode(secret, current).Should().BeTrue();
        service.VerifyCode(secret, previous).Should().BeTrue();
    }

    [Fact]
    public void Recovery_codes_have_expected_shape_and_hashes_are_one_way()
    {
        var service = new RecoveryCodeService();

        var codes = service.GenerateCodes(20);

        codes.Should().HaveCount(20);
        codes.Should().OnlyContain(code => code.Length == 17 && code.Count(c => c == '-') == 2);
        codes.Select(service.HashCode).Should().OnlyContain(hash => hash.Length == 64);
        service.HashCode(codes[0]).Should().Be(service.HashCode(codes[0]));
        service.HashCode(codes[0]).Should().NotBe(service.HashCode(codes[0] + "x"));
    }

    [Fact]
    public void Recovery_codes_support_empty_generation_and_known_sha256_vector()
    {
        var service = new RecoveryCodeService();

        service.GenerateCodes(0).Should().BeEmpty();
        service.HashCode("abc").Should().Be(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Scim_scope_checks_require_authenticated_identity_and_exact_scope()
    {
        var authenticated = Principal("scim.read scim.write");
        var readOnly = Principal("scim.read");
        var unauthenticated = Principal("scim.write", authenticated: false);

        ScimAuthorization.HasProvisioningScope(authenticated).Should().BeTrue();
        ScimAuthorization.HasScope(readOnly, ScimAuthorization.ReadScope).Should().BeTrue();
        ScimAuthorization.HasScope(readOnly, ScimAuthorization.WriteScope).Should().BeFalse();
        ScimAuthorization.HasProvisioningScope(unauthenticated).Should().BeFalse();
        ScimAuthorization.HasScope(authenticated, "scim").Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(string scopes, bool authenticated = true) =>
        new(new ClaimsIdentity(
            [new Claim("scope", scopes)],
            authenticated ? "Bearer" : null));

    private static string GenerateCode(string secret, long counter)
    {
        var bytes = DecodeBase32(secret);
        Span<byte> counterBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(counterBytes, counter);
        if (BitConverter.IsLittleEndian) counterBytes.Reverse();

        using var hmac = new HMACSHA1(bytes);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var value = (hash[offset] & 0x7f) << 24 |
                    (hash[offset + 1] & 0xff) << 16 |
                    (hash[offset + 2] & 0xff) << 8 |
                    (hash[offset + 3] & 0xff);
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var bitCount = 0;
        var bytes = new List<byte>();
        foreach (var character in input)
        {
            bits = (bits << 5) | alphabet.IndexOf(character);
            bitCount += 5;
            if (bitCount < 8) continue;
            bitCount -= 8;
            bytes.Add((byte)((bits >> bitCount) & 0xff));
        }

        return [.. bytes];
    }
}
