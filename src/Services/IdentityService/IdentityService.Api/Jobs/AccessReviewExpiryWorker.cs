using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Api.Jobs;

/// <summary>
/// Expires overdue access reviews, removes the reviewed roles, and revokes the
/// subject's sessions. Request-time checks remain authoritative; this worker
/// closes the governance lifecycle when no reviewer acts before the deadline.
/// </summary>
public sealed class AccessReviewExpiryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepLockTtl = TimeSpan.FromMinutes(10);
    private const string SweepLockKey = "identity:access-review-expiry:sweep";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityRedisLock _distributedLock;
    private readonly ILogger<AccessReviewExpiryWorker> _logger;

    public AccessReviewExpiryWorker(
        IServiceScopeFactory scopeFactory,
        IdentityRedisLock distributedLock,
        ILogger<AccessReviewExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access review expiry worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireReviewsAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Access review expiry sweep failed");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    public async Task<int> ExpireReviewsAsync(CancellationToken ct)
    {
        await using var sweepLease = await _distributedLock.TryAcquireAsync(SweepLockKey, SweepLockTtl);
        if (sweepLease is null)
        {
            _logger.LogDebug("Access review expiry sweep is already running on another replica");
            return 0;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var tokenBlacklist = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var now = DateTime.UtcNow;
        var reviews = await db.AccessReviews
            .Where(item => item.Status == "pending" && item.DueAt <= now)
            .OrderBy(item => item.DueAt)
            .Take(100)
            .ToListAsync(ct);
        var expired = 0;

        foreach (var review in reviews)
        {
            try
            {
                var subject = await userManager.FindByIdAsync(review.SubjectUserId.ToString());
                if (subject is null)
                    throw new InvalidOperationException($"Access review subject {review.SubjectUserId} was not found.");

                var roleIds = JsonSerializer.Deserialize<string[]>(review.RoleIdsJson) ?? [];
                var roleNames = new List<string>();
                foreach (var roleId in roleIds)
                {
                    var role = await roleManager.FindByIdAsync(roleId);
                    if (role is not null && role.Name is not null)
                        roleNames.Add(role.Name);
                }

                if (roleNames.Count > 0)
                {
                    var result = await userManager.RemoveFromRolesAsync(subject, roleNames);
                    if (!result.Succeeded)
                        throw new InvalidOperationException(
                            $"Unable to remove roles for overdue access review {review.Id}: " +
                            string.Join(", ", result.Errors.Select(error => error.Code)));
                }

                await tokenBlacklist.RevokeAllUserTokensAsync(review.SubjectUserId.ToString(), ct);
                review.Status = "expired";
                review.DecisionReason = "Access revoked automatically after the review due date expired.";
                review.DecidedAt = now;
                await db.SaveChangesAsync(ct);
                await audit.LogPhiAccessAsync(new PhiAuditEntry
                {
                    UserId = review.SubjectUserId.ToString(),
                    ResourceType = nameof(AccessReview),
                    ResourceId = review.Id.ToString("D"),
                    Action = "EXPIRE",
                    HttpMethod = "WORKER",
                    Path = "access-reviews/expiry"
                }, ct);
                expired++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire access review {ReviewId}; it will be retried", review.Id);
            }
        }

        return expired;
    }
}
