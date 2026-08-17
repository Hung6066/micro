using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class OpenIddictPopulateTokenClaimsTests
{
    [Fact]
    public async Task Returns_without_mutation_when_principal_is_missing()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(OpenIddictConstants.GrantTypes.Password, principal: null);

        await Handler(db).HandleAsync(context);

        context.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Token_exchange_does_not_repopulate_claims()
    {
        await using var db = TestApplicationDbContext.Create();
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, Guid.NewGuid().ToString("D")));
        var principal = new ClaimsPrincipal(identity);
        var context = Context(AuthorizationConstants.GrantTypes.TokenExchange, principal);

        await Handler(db).HandleAsync(context);

        identity.Claims.Should().ContainSingle();
    }

    [Fact]
    public async Task Client_credentials_without_role_adds_no_workload_permissions()
    {
        await using var db = TestApplicationDbContext.Create();
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, "workload-client"));
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, new ClaimsPrincipal(identity));

        await Handler(db).HandleAsync(context);

        identity.FindFirst("principal_type")!.Value.Should().Be("workload");
        identity.FindFirst("permissions").Should().BeNull();
        identity.FindFirst("workload_role_id").Should().BeNull();
    }

    [Fact]
    public async Task Human_principal_gets_profile_roles_and_password_amr_claims()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User
        {
            Id = Guid.NewGuid(), FirstName = "An", LastName = "Nguyen", LicenseNumber = "LIC-42",
            Email = "an@example.test", UserName = "an@example.test"
        };
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.FindByIdAsync(user.Id.ToString("D"))).ReturnsAsync(user);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Clinician", "Auditor"]);
        manager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync([]);
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString("D")));
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));

        await new CustomPopulateTokenClaims(manager.Object, db, NullLogger<CustomPopulateTokenClaims>.Instance).HandleAsync(context);

        identity.FindFirst(AuthorizationConstants.Claims.PrincipalType)!.Value.Should().Be(AuthorizationConstants.PrincipalTypes.Human);
        identity.FindFirst("fullName")!.Value.Should().Be("Nguyen An");
        identity.FindFirst("licenseNumber")!.Value.Should().Be("LIC-42");
        identity.FindAll(OpenIddictConstants.Claims.Role).Select(c => c.Value).Should().BeEquivalentTo("Clinician", "Auditor");
        identity.FindFirst("amr")!.Value.Should().Be("pwd");
        identity.FindFirst("scope")!.Value.Should().Be("hishop:permissions");
    }

    private static CustomPopulateTokenClaims Handler(IApplicationDbContext db) =>
        new(CreateUserManager(), db, NullLogger<CustomPopulateTokenClaims>.Instance);

    private static OpenIddictServerEvents.HandleTokenRequestContext Context(string grantType, ClaimsPrincipal? principal)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { GrantType = grantType }
        };
        var context = new OpenIddictServerEvents.HandleTokenRequestContext(transaction);
        context.Principal = principal;
        return context;
    }

    private static UserManager<User> CreateUserManager()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new UserManager<User>(
            new UserStoreStub(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            NullLogger<UserManager<User>>.Instance);
    }

    private sealed class UserStoreStub : IUserStore<User>
    {
        public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString("D"));
        public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }
}
