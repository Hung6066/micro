using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>
/// Per-account brute-force protection for the interactive password login.
/// <para>
/// <see cref="User.LockoutEnd"/> hides the ASP.NET Identity property of the same
/// name, so <c>SignInManager</c> lockout never reaches the mapped column. This
/// guard uses the mapped columns directly and increments atomically in SQL so
/// parallel guesses cannot lose a failed attempt.
/// </para>
/// </summary>
public sealed class LoginAttemptGuard(IdentityDbContext db, ILogger<LoginAttemptGuard> logger)
{
    public const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly Lazy<string> DummyHash = new(() =>
        Hasher.HashPassword(new User(), Guid.NewGuid().ToString("N")));

    public static bool IsLockedOut(User user) =>
        user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTime.UtcNow;

    /// <summary>
    /// Spends the same hashing time as a real verification so an unknown or
    /// disabled account cannot be told apart from a wrong password by timing.
    /// </summary>
    public static void VerifyDummyPassword(string password) =>
        Hasher.VerifyHashedPassword(new User(), DummyHash.Value, password);

    public async Task RecordFailureAsync(User user, CancellationToken cancellationToken)
    {
        await db.Users
            .Where(item => item.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                item => item.FailedLoginAttempts,
                item => item.FailedLoginAttempts + 1), cancellationToken);
        var attempts = await db.Users.AsNoTracking()
            .Where(item => item.Id == user.Id)
            .Select(item => item.FailedLoginAttempts)
            .SingleAsync(cancellationToken);
        if (attempts < MaxFailedAttempts)
            return;

        DateTime? lockedUntil = DateTime.UtcNow.Add(LockoutDuration);
        await db.Users
            .Where(item => item.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LockoutEnd, lockedUntil)
                .SetProperty(item => item.FailedLoginAttempts, 0), cancellationToken);
        logger.LogWarning(
            "Account {UserId} locked for {Minutes} minutes after {Attempts} failed interactive sign-in attempts.",
            user.Id, LockoutDuration.TotalMinutes, MaxFailedAttempts);
    }

    public async Task RecordSuccessAsync(User user, CancellationToken cancellationToken)
    {
        if (user.FailedLoginAttempts == 0)
            return;
        await db.Users
            .Where(item => item.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedLoginAttempts, 0), cancellationToken);
    }
}
