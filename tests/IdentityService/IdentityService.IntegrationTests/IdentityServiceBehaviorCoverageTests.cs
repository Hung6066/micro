using System.Security.Cryptography;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class IdentityServiceBehaviorCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Register_login_and_lookup_cover_identity_service_happy_paths()
    {
        var identity = fixture.Services.GetRequiredService<IIdentityService>();
        var roleManager = fixture.Services.GetRequiredService<RoleManager<Role>>();
        if (!await roleManager.RoleExistsAsync("Provider"))
            await roleManager.CreateAsync(new Role { Name = "Provider", CreatedAt = DateTime.UtcNow });
        var email = $"coverage-{Guid.NewGuid():N}@test.test";
        var password = "Coverage-password!123";

        var registered = await identity.RegisterAsync(new RegisterRequest(
            Email: email,
            Password: password,
            FirstName: "Coverage",
            LastName: "User"));

        Assert.Equal(email, registered.User.Email);
        var lookedUp = await identity.GetUserByIdAsync(registered.User.Id);
        Assert.Equal(email, lookedUp.Email);

        var loggedIn = await identity.LoginAsync(new LoginRequest(Email: email, Password: password));
        Assert.Equal(registered.User.Id, loggedIn.User.Id);
        await identity.LogoutAsync(loggedIn.RefreshToken);
    }

    [Fact]
    public async Task Login_rejects_missing_identifier_and_invalid_password()
    {
        var identity = fixture.Services.GetRequiredService<IIdentityService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            identity.LoginAsync(new LoginRequest(Password: "invalid")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            identity.LoginAsync(new LoginRequest(Email: IdentityTestCredentials.Email, Password: "invalid")));
    }

    [Fact]
    public async Task Refresh_and_lookup_fail_closed_for_invalid_inputs()
    {
        var identity = fixture.Services.GetRequiredService<IIdentityService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            identity.RefreshTokenAsync(new RefreshTokenRequest("invalid", "invalid")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            identity.GetUserByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            identity.GeneratePasswordResetTokenAsync($"missing-{Guid.NewGuid():N}@test.test"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            identity.GenerateEmailConfirmationTokenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Password_and_email_recovery_reject_invalid_tokens()
    {
        var identity = fixture.Services.GetRequiredService<IIdentityService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            identity.ResetPasswordAsync(IdentityTestCredentials.Email, "invalid-token", "New-password!123"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            identity.ConfirmEmailAsync(IdentityTestCredentials.Email, "invalid-token"));

        var userManager = fixture.Services.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(IdentityTestCredentials.Email);
        Assert.NotNull(user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            identity.ChangePasswordAsync(user!.Id, "wrong-current", "New-password!123"));
    }

    [Fact]
    public async Task Deactivated_user_cannot_login_and_can_be_restored()
    {
        var identity = fixture.Services.GetRequiredService<IIdentityService>();
        var userManager = fixture.Services.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(IdentityTestCredentials.Email);
        Assert.NotNull(user);

        user!.IsActive = false;
        await userManager.UpdateAsync(user);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                identity.LoginAsync(new LoginRequest(Email: IdentityTestCredentials.Email, Password: IdentityTestCredentials.Password)));
        }
        finally
        {
            user.IsActive = true;
            await userManager.UpdateAsync(user);
        }
    }
}
