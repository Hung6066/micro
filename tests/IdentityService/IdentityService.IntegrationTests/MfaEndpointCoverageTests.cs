using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class MfaEndpointCoverageTests
{
    private const string MfaStatusRoute = IdentityApiRoutes.Mfa + "/status";
    private readonly IdentityServiceTestFixture _fixture;

    public MfaEndpointCoverageTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Methods_WithoutPendingOidcChallenge_Returns401()
    {
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Mfa + "/methods");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithoutAuthentication_Returns401()
    {
        var response = await _fixture.AnonymousClient.GetAsync(MfaStatusRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_ForAuthenticatedUserWithoutEnrollment_ReturnsDisabledContract()
    {
        var (session, _) = await CreateLoggedInUserAsync("mfa-status-disabled");

        var response = await session.GetWithCookiesAsync(MfaStatusRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("enabled").GetBoolean());
        Assert.False(body.GetProperty("requiresMfa").GetBoolean());
        Assert.Equal(0, body.GetProperty("recoveryCodesRemaining").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("enrolledAt").ValueKind);
    }

    [Fact]
    public async Task Status_ForEnabledEnrollment_RequiresMfaWhenSessionHasNoOtpAmr()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-status-enabled");

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.UserMfas.Add(new UserMfa
        {
            UserId = userId,
            SecretKey = "test-secret",
            IsEnabled = true,
            EnrolledAt = DateTime.UtcNow,
            RecoveryCodes = ["one", "two", "three"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await session.GetWithCookiesAsync(MfaStatusRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("enabled").GetBoolean());
        Assert.True(body.GetProperty("requiresMfa").GetBoolean());
        Assert.Equal(3, body.GetProperty("recoveryCodesRemaining").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("enrolledAt").ValueKind);
    }

    [Fact]
    public async Task Enroll_WhenMfaAlreadyEnabled_ReturnsProblem400()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-enroll-enabled");

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.UserMfas.Add(new UserMfa
            {
                UserId = userId,
                SecretKey = "test-secret",
                IsEnabled = true,
                RecoveryCodes = [],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        session.RateLimitKey = $"mfa-enroll-enabled-{Guid.NewGuid():N}";
        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"errorCode\":\"invalid_mfa_state\"", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recover_WhenUserHasNoEnrollment_ReturnsProblem400()
    {
        var (session, _) = await CreateLoggedInUserAsync("mfa-recover-not-enrolled");
        session.RateLimitKey = $"mfa-recover-not-enrolled-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaRecover,
            new { recoveryCode = "unused-code" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WhenUserHasNoEnrollment_ReturnsProblem400()
    {
        var (session, _) = await CreateLoggedInUserAsync("mfa-verify-not-enrolled");
        session.RateLimitKey = $"mfa-verify-not-enrolled-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaVerify, new { code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"errorCode\":\"invalid_mfa_state\"", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_WithBlankCodeAndEnrollment_ReturnsProblem400()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-verify-blank");
        await SeedEnrollmentAsync(userId, "known-code");
        session.RateLimitKey = $"mfa-verify-blank-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaVerify, new { code = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recover_WithInvalidCode_ReturnsProblem400()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-recover-invalid");
        await SeedEnrollmentAsync(userId, "known-recovery-code");
        session.RateLimitKey = $"mfa-recover-invalid-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaRecover,
            new { recoveryCode = "wrong-recovery-code" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Recover_WithBlankCodeAndEnrollment_ReturnsProblem400()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-recover-blank");
        await SeedEnrollmentAsync(userId, "known-recovery-code");
        session.RateLimitKey = $"mfa-recover-blank-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaRecover, new { recoveryCode = "" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithCurrentTotpCode_enablesMfa_and_returns_bff_safe_contract()
    {
        var (session, _) = await CreateLoggedInUserAsync("mfa-verify-valid");
        session.RateLimitKey = $"mfa-verify-valid-{Guid.NewGuid():N}";
        var enroll = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);
        var enrollment = await enroll.Content.ReadFromJsonAsync<JsonElement>();
        var secret = enrollment.GetProperty("secretKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secret));

        var verify = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaVerify,
            new { code = GenerateCurrentTotp(secret!) });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("requiresMfa").GetBoolean());
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.False(body.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Recover_WithValidCode_RequiresCompletedMfa()
    {
        var (session, userId) = await CreateLoggedInUserAsync("mfa-recover-valid");
        const string recoveryCode = "valid-recovery-code";
        await SeedEnrollmentAsync(userId, recoveryCode);
        session.RateLimitKey = $"mfa-recover-valid-{Guid.NewGuid():N}";

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaRecover,
            new { recoveryCode });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedEnrollmentAsync(Guid userId, string recoveryCode)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var recovery = scope.ServiceProvider.GetRequiredService<RecoveryCodeService>();
        db.UserMfas.Add(new UserMfa
        {
            UserId = userId,
            SecretKey = "test-secret",
            IsEnabled = true,
            EnrolledAt = DateTime.UtcNow,
            RecoveryCodes = [recovery.HashCode(recoveryCode)],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<(SessionClient Session, Guid UserId)> CreateLoggedInUserAsync(string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Mfa",
            LastName = "Coverage",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(user, IdentityTestCredentials.Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

        var session = _fixture.CreateSessionClient();
        var login = await session.LoginAsync(email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (session, user.Id);
    }

    private static string GenerateCurrentTotp(string secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var bits = 0;
        var bitCount = 0;
        foreach (var c in secret.Trim().ToUpperInvariant().TrimEnd('='))
        {
            var index = alphabet.IndexOf(c);
            if (index < 0) continue;
            bits = (bits << 5) | index;
            bitCount += 5;
            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bits >> bitCount) & 0xff));
            }
        }

        var counter = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
        using var hmac = new HMACSHA1(bytes.ToArray());
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) |
                    ((hash[offset + 1] & 0xff) << 16) |
                    ((hash[offset + 2] & 0xff) << 8) |
                    (hash[offset + 3] & 0xff);
        return (value % 1_000_000).ToString("000000");
    }
}
