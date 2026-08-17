using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class PasswordHistoryTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public PasswordHistoryTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PasswordChange_PersistsPriorHash_AndRejectsReuseAfterReload()
    {
        var email = $"history-{Guid.NewGuid():N}@test.test";
        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User { UserName = email, Email = email, EmailConfirmed = true, FirstName = "History", LastName = "Test" };
            Assert.True((await users.CreateAsync(user, "Original@123")).Succeeded);
            var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await service.ChangePasswordAsync(user.Id, "Original@123", "Changed@123");
        }

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email);
            Assert.Equal(1, await db.UserPasswordHistories.CountAsync(x => x.UserId == user.Id && x.PasswordHash != ""));
            var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ChangePasswordAsync(user.Id, "Changed@123", "Original@123"));
        }
    }
}
