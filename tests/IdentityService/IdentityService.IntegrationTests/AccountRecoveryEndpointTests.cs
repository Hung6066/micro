using System.Net;
using System.Net.Http.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AccountRecoveryEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public AccountRecoveryEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ForgotPassword_requires_email()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ForgotPassword, new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_does_not_disclose_unknown_email()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ForgotPassword,
            new { email = $"missing-{Guid.NewGuid():N}@example.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_requires_all_fields()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ResetPassword,
            new { email = "user@example.test", token = "", newPassword = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_rejects_unknown_email_and_token_without_disclosing_account()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ResetPassword,
            new { email = $"missing-{Guid.NewGuid():N}@example.test", token = "invalid-token", newPassword = "NewPassword!123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyEmail_rejects_unknown_email_and_token_without_disclosing_account()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.VerifyEmail,
            new { email = $"missing-{Guid.NewGuid():N}@example.test", token = "invalid-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyEmail_requires_email_and_token()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.VerifyEmail,
            new { email = "", token = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_recovery_operations_require_authentication()
    {
        var change = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ChangePassword,
            new { currentPassword = "old", newPassword = "new" });
        var verifyMessage = await _fixture.AnonymousClient.PostAsync(IdentityApiRoutes.SendEmailVerification, null);
        var sessions = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Sessions);

        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, verifyMessage.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sessions.StatusCode);
    }

    [Fact]
    public async Task Authenticated_recovery_and_session_operations_fail_closed_and_revoke()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.ChangePassword, new { currentPassword = "", newPassword = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.ChangePassword, new
            {
                currentPassword = IdentityTestCredentials.Password,
                newPassword = IdentityTestCredentials.Password
            })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.SendEmailVerification, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.VerifyEmail, new { email = IdentityTestCredentials.Email, token = "invalid" })).StatusCode);

        var sessions = await session.GetWithCookiesAsync(IdentityApiRoutes.Sessions);
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        var current = session.GetCookieValue("hishop_sid");
        Assert.False(string.IsNullOrWhiteSpace(current));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.Sessions}/{current}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.Sessions}/non-current-session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.DeleteWithCookiesAsync(IdentityApiRoutes.Sessions)).StatusCode);
    }

    [Fact]
    public async Task Authenticated_change_password_rejects_wrong_current_password()
    {
        using var session = await LoginAsync();
        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.ChangePassword,
            new { currentPassword = "Wrong-password!123", newPassword = "Another-password!123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
