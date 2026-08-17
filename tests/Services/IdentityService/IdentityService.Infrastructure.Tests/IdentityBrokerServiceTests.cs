using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityBrokerServiceTests
{
    [Fact]
    public async Task FindOrCreateExternalUser_without_email_fails_closed()
    {
        var userManager = CreateUserManager();
        var service = CreateService(userManager);

        var result = await service.FindOrCreateExternalUserAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "No Email")])), "Google");

        result.User.Should().BeNull();
        result.IsNew.Should().BeFalse();
        result.Error.Should().Contain("email");
        userManager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task FindOrCreateExternalUser_returns_existing_linked_login_without_mutation()
    {
        var existing = new User { Id = Guid.NewGuid(), Email = "linked@example.test" };
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByLoginAsync("Google", "provider-key"))
            .ReturnsAsync(existing);
        var service = CreateService(userManager);

        var result = await service.FindOrCreateExternalUserAsync(Principal("linked@example.test", "provider-key"), "Google");

        result.User.Should().BeSameAs(existing);
        result.IsNew.Should().BeFalse();
        result.Error.Should().BeNull();
        userManager.Verify(x => x.AddLoginAsync(It.IsAny<User>(), It.IsAny<UserLoginInfo>()), Times.Never);
        userManager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task FindOrCreateExternalUser_links_provider_to_existing_email()
    {
        var existing = new User { Id = Guid.NewGuid(), Email = "existing@example.test" };
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByLoginAsync("Microsoft", "provider-key")).ReturnsAsync((User?)null);
        userManager.Setup(x => x.FindByEmailAsync(existing.Email!)).ReturnsAsync(existing);
        userManager.Setup(x => x.AddLoginAsync(existing, It.IsAny<UserLoginInfo>()))
            .ReturnsAsync(IdentityResult.Success);
        var service = CreateService(userManager);

        var result = await service.FindOrCreateExternalUserAsync(Principal(existing.Email!, "provider-key"), "Microsoft");

        result.User.Should().BeSameAs(existing);
        result.IsNew.Should().BeFalse();
        result.Error.Should().BeNull();
        userManager.Verify(x => x.AddLoginAsync(existing,
            It.Is<UserLoginInfo>(i => i.LoginProvider == "Microsoft" && i.ProviderKey == "provider-key")), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateExternalUser_returns_identity_errors_when_provisioning_fails()
    {
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByLoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((User?)null);
        userManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        userManager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "email is blocked" }));
        var service = CreateService(userManager);

        var result = await service.FindOrCreateExternalUserAsync(Principal("blocked@example.test", "key"), "Google");

        result.User.Should().BeNull();
        result.IsNew.Should().BeFalse();
        result.Error.Should().Be("email is blocked");
        userManager.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), "Provider"), Times.Never);
    }

    [Fact]
    public void TransformClaims_adds_provider_and_provider_specific_claims()
    {
        var service = CreateService(CreateUserManager());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Email, "doctor@example.test"),
            new Claim(ClaimTypes.Name, "Dr. Example"),
            new Claim(ClaimTypes.GivenName, "Dr."),
            new Claim(ClaimTypes.Surname, "Example"),
            new Claim("picture", "https://cdn.example.test/photo"),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", "tenant-1")
        ]));

        var google = service.TransformClaims(principal, "Google");
        google.Should().Contain(x => x.Type == "identity_provider" && x.Value == "Google");
        google.Should().Contain(x => x.Type == "auth_method" && x.Value == "federated");
        google.Should().Contain(x => x.Type == "picture");

        var microsoft = service.TransformClaims(principal, "Microsoft");
        microsoft.Should().Contain(x => x.Type == "ms_tenant_id" && x.Value == "tenant-1");
    }

    private static ClaimsPrincipal Principal(string email, string providerKey) =>
        new(new ClaimsIdentity([
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "External User"),
            new Claim(ClaimTypes.NameIdentifier, providerKey)
        ]));

    private static Mock<UserManager<User>> CreateUserManager() => new(
        Mock.Of<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    private static IdentityBrokerService CreateService(Mock<UserManager<User>> userManager) =>
        new(userManager.Object, null!, NullLogger<IdentityBrokerService>.Instance);
}
