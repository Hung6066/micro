using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.IdentityService.Api.Risk;
using His.Hope.IdentityService.Application.Risk;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>Zero Trust 3.5 R0: sign-in risk events (report-only) and the IAM → Risk read API.</summary>
[Collection("IdentityServiceIntegration")]
public sealed class RiskEndpointTests(IdentityServiceTestFixture fixture)
{
    private static readonly string Root = $"{IdentityApiRoutes.AdminIam}/risk";

    [Fact]
    public async Task Anonymous_callers_cannot_read_risk()
    {
        foreach (var route in new[] { $"{Root}/events", $"{Root}/summary" })
        {
            var response = await fixture.AnonymousClient.GetAsync(route);
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect, route);
        }
    }

    [Fact]
    public async Task Invalid_queries_are_rejected()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-60).ToString("O"));
        foreach (var query in new[] { "band=extreme", "pageSize=0", "pageSize=500", "page=0", $"from={from}" })
        {
            var response = await session.GetWithCookiesAsync($"{Root}/events?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_risk_request",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
        }
    }

    [Fact]
    public async Task Browser_sign_in_writes_scored_events_and_admin_would_step_up_is_logged_only()
    {
        var user = await CreateUserAsync(admin: true);
        await SeedAllHoursAsync(user.Id);

        var browser = fixture.CreateSessionClient();
        var failed = await PostLoginAsync(browser, user.Email!, "Wrong-password-1!");
        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        Assert.Contains("error=invalid_credentials", failed.Headers.Location!.OriginalString);

        var succeeded = await PostLoginAsync(browser, user.Email!, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.Redirect, succeeded.StatusCode);
        Assert.DoesNotContain("error=", succeeded.Headers.Location!.OriginalString);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var events = await db.SecurityEvents.AsNoTracking().Where(item => item.UserId == user.Id)
            .OrderBy(item => item.Timestamp).ToListAsync();

        var failure = Assert.Single(events, item => item.EventType == RiskEventTypes.LoginFailed);
        var failureDetails = RiskEventDetails.TryParse(failure.Details)!;
        Assert.Contains(failureDetails.Risk.Reasons, reason => reason.Code == RiskReasonCodes.FailedPassword && reason.Points == 10);
        Assert.Equal("invalid_password", failureDetails.Outcome);

        // One failed password (+10) and an unfamiliar browser (+20): medium, so an admin would be stepped up.
        var success = Assert.Single(events, item => item.EventType == RiskEventTypes.LoginSuccess);
        var successDetails = RiskEventDetails.TryParse(success.Details)!;
        Assert.Equal(30, successDetails.Risk.Score);
        Assert.Equal(RiskBands.Medium, successDetails.Risk.Band);
        Assert.Contains(successDetails.Risk.Reasons, reason => reason.Code == RiskReasonCodes.UnfamiliarDevice);
        Assert.DoesNotContain(successDetails.Risk.Reasons, reason => reason.Code == RiskReasonCodes.OddHours);
        var wouldAct = Assert.Single(events, item => item.EventType == RiskEventTypes.WouldStepUp);
        Assert.Equal("report_only", RiskEventDetails.TryParse(wouldAct.Details)!.Outcome);
        Assert.True(await db.SecuritySignalOutbox.AnyAsync(item => item.Subject == user.Id.ToString() && item.EventType == RiskEventTypes.LoginSuccess));

        using var admin = await fixture.CreateAuthenticatedSessionAsync();
        var page = await admin.GetWithCookiesAsync($"{Root}/events?user={user.Id}&band=medium&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, item => Assert.Equal("medium", item.GetProperty("band").GetString()));
        Assert.Contains(items, item => item.GetProperty("eventType").GetString() == RiskEventTypes.LoginSuccess
            && item.GetProperty("score").GetInt32() == 30
            && item.GetProperty("reasons").EnumerateArray().Any(reason => reason.GetProperty("code").GetString() == RiskReasonCodes.FailedPassword));

        var low = await admin.GetWithCookiesAsync($"{Root}/events?user={user.Id}&band=low");
        var lowBody = await low.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, lowBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(RiskEventTypes.LoginFailed, lowBody.GetProperty("items")[0].GetProperty("eventType").GetString());

        var summary = await admin.GetWithCookiesAsync($"{Root}/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        var windows = (await summary.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("windows");
        Assert.True(windows.GetProperty("24h").GetProperty("bands").GetProperty("medium").GetInt32() >= 1);
        Assert.True(windows.GetProperty("24h").GetProperty("wouldStepUp").GetInt32() >= 1);
        Assert.True(windows.GetProperty("7d").GetProperty("total").GetInt32() >= 2);
    }

    [Fact]
    public async Task Recorder_learns_prefixes_from_successes_only_and_flags_a_new_one()
    {
        var user = await CreateUserAsync(admin: false);
        await SeedAllHoursAsync(user.Id);
        var recorder = fixture.Services.GetRequiredService<ISignInRiskRecorder>();
        var home = new SignInRiskOrigin(IPAddress.Parse("198.51.100.20"), "test-agent");
        var away = new SignInRiskOrigin(IPAddress.Parse("203.0.113.9"), "test-agent");

        await recorder.RecordAsync(home, user, new(RiskEventTypes.LoginSuccess, ["pwd"], 0, 0));
        await recorder.RecordAsync(away, user, new(RiskEventTypes.LoginFailed, ["pwd"], 0, 0));
        await recorder.RecordAsync(away, user, new(RiskEventTypes.LoginSuccess, ["passkey"], 0, 0));
        await recorder.RecordAsync(away, user, new(RiskEventTypes.LoginSuccess, ["pwd"], 0, 0));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var events = await db.SecurityEvents.AsNoTracking().Where(item => item.UserId == user.Id)
            .OrderBy(item => item.Timestamp).ToListAsync();
        var details = events.Select(item => RiskEventDetails.TryParse(item.Details)!).ToList();

        Assert.Equal(4, details.Count);
        Assert.Equal(0, details[0].Risk.Score); // first sign-in: no baseline yet
        Assert.Equal("198.51.100.0/24", details[0].IpPrefix);
        Assert.Equal("198.51.100.0/24", events[0].IpAddress); // prefix only, never the raw address
        Assert.Contains(details[1].Risk.Reasons, reason => reason.Code == RiskReasonCodes.NewIpPrefix);
        Assert.Contains(details[2].Risk.Reasons, reason => reason.Code == RiskReasonCodes.NewIpPrefix); // a failure taught nothing
        Assert.Contains(details[2].Risk.Reasons, reason => reason.Code == RiskReasonCodes.Passkey && reason.Points == -15);
        Assert.Equal(0, details[2].Risk.Score);
        Assert.DoesNotContain(details[3].Risk.Reasons, reason => reason.Code == RiskReasonCodes.NewIpPrefix);
        Assert.DoesNotContain(events, item => item.EventType.StartsWith("risk_policy_", StringComparison.Ordinal)); // not an admin

        var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var prefixes = await redis.SortedSetRangeByRankAsync(RiskSignalKeys.IpPrefixes(user.Id));
        Assert.Equal(new[] { "198.51.100.0/24", "203.0.113.0/24" }, prefixes.Select(value => value.ToString()).Order());
        Assert.True((await redis.KeyTimeToLiveAsync(RiskSignalKeys.IpPrefixes(user.Id))) is { TotalDays: > 29 and <= 30 });
    }

    [Fact]
    public async Task Recorder_fails_open_when_the_signal_store_breaks()
    {
        var user = await CreateUserAsync(admin: false);
        var recorder = new SignInRiskRecorder(
            new RiskScorer(new RiskScoringOptions()),
            new ThrowingSignalStore(),
            fixture.Services.GetRequiredService<IConnectionMultiplexer>(),
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RiskScoringOptions()),
            TimeProvider.System,
            NullLogger<SignInRiskRecorder>.Instance);

        await recorder.RecordAsync(new SignInRiskOrigin(IPAddress.Loopback, "ua"), user, new(RiskEventTypes.LoginSuccess, ["pwd"]));
        recorder.RecordDetached(new SignInRiskOrigin(IPAddress.Loopback, "ua"), user, new(RiskEventTypes.LoginFailed, ["pwd"]));
        Assert.Equal(0, await recorder.TakePendingPrimaryAsync("missing"));
        await recorder.RememberPendingPrimaryAsync("missing", 2, TimeSpan.FromMinutes(1));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await db.SecurityEvents.AnyAsync(item => item.UserId == user.Id && item.EventType == RiskEventTypes.LoginSuccess));
    }

    private async Task<User> CreateUserAsync(bool admin)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"risk-{suffix}",
            Email = $"risk-{suffix}@example.com",
            FirstName = "Risk",
            LastName = "Test",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var create = await userManager.CreateAsync(user, IdentityTestCredentials.Password);
        Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));
        if (admin)
        {
            var role = await userManager.AddToRoleAsync(user, SignInRiskRecorder.AdminRole);
            Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
        }
        return user;
    }

    /// <summary>A learned histogram that covers every hour, so the wall clock never adds "odd hours".</summary>
    private async Task SeedAllHoursAsync(Guid userId)
    {
        var redis = fixture.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await redis.HashSetAsync(RiskSignalKeys.SignInHours(userId),
            Enumerable.Range(0, 24).Select(hour => new HashEntry(hour, 1)).ToArray());
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(SessionClient browser, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = password,
                ["returnUrl"] = "/"
            })
        };
        request.Headers.Add("X-RateLimit-Key", $"risk-login-{Guid.NewGuid():N}");
        var response = await browser.InnerClient.SendAsync(request);
        browser.ApplySetCookieHeaders(response);
        return response;
    }

    private sealed class ThrowingSignalStore : ISignInRiskSignalStore
    {
        public Task<SignInRiskHistory> GetHistoryAsync(Guid userId, string? ipPrefix, CancellationToken cancellationToken = default) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down");
        public Task RecordSuccessAsync(Guid userId, string? ipPrefix, int localHour, CancellationToken cancellationToken = default) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down");
        public Task<bool> HasRecentRefreshTokenReuseAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down");
        public Task RememberPendingPrimaryAsync(string pendingId, int failedPasswordAttempts, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down");
        public Task<int> TakePendingPrimaryAsync(string pendingId, CancellationToken cancellationToken = default) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down");
    }
}
