using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.Risk;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Risk;

/// <summary>
/// One browser sign-in attempt to score. Null counts are read from their source
/// (<c>User.FailedLoginAttempts</c>, the MFA failure counter); callers pass them
/// explicitly when the source was already reset by a successful step.
/// </summary>
public sealed record SignInRiskAttempt(
    string EventType,
    IReadOnlyCollection<string> Methods,
    int? FailedPasswordAttempts = null,
    int? FailedMfaAttempts = null,
    bool UnfamiliarDevice = false,
    string? Outcome = null);

/// <summary>What the recorder needs from the request, captured so it can also run after the response.</summary>
public sealed record SignInRiskOrigin(System.Net.IPAddress? RemoteIpAddress, string? UserAgent)
{
    public static SignInRiskOrigin From(HttpContext context) =>
        new(context.Connection.RemoteIpAddress, context.Request.Headers.UserAgent.ToString());
}

public interface ISignInRiskRecorder
{
    /// <summary>Scores the attempt and writes the risk SecurityEvent. Never throws.</summary>
    Task RecordAsync(SignInRiskOrigin origin, User user, SignInRiskAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records without delaying the response (used where the response time must not reveal
    /// whether an account exists or is locked). Never throws.
    /// </summary>
    void RecordDetached(SignInRiskOrigin origin, User user, SignInRiskAttempt attempt);

    /// <summary>Carries the password-failure count of a primary sign-in to its MFA completion. Never throws.</summary>
    Task RememberPendingPrimaryAsync(string pendingId, int failedPasswordAttempts, TimeSpan lifetime);

    /// <summary>Returns (and forgets) the carried count; 0 when unknown. Never throws.</summary>
    Task<int> TakePendingPrimaryAsync(string pendingId);
}

/// <summary>
/// Zero Trust 3.5 R0 sign-in hook: gathers the signals, scores them and writes
/// <c>login_success</c> / <c>login_failed</c> / <c>mfa_failed</c> security events with
/// <c>Details.risk = {score, band, reasons}</c>, plus <c>risk_policy_would_step_up</c> /
/// <c>risk_policy_would_deny</c> for admin accounts. Report-only and fail-open: any error
/// is logged as a warning and the sign-in continues unchanged.
/// </summary>
public sealed class SignInRiskRecorder(
    IRiskScorer scorer,
    ISignInRiskSignalStore store,
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopes,
    IOptions<RiskScoringOptions> options,
    TimeProvider clock,
    ILogger<SignInRiskRecorder> logger) : ISignInRiskRecorder
{
    public const string AdminRole = "Admin";
    private readonly Lazy<TimeZoneInfo> zone = new(() => OddHoursRule.ResolveZone(options.Value.TimeZone));

    public void RecordDetached(SignInRiskOrigin origin, User user, SignInRiskAttempt attempt) =>
        _ = Task.Run(() => RecordAsync(origin, user, attempt, CancellationToken.None));

    public async Task RecordAsync(SignInRiskOrigin origin, User user, SignInRiskAttempt attempt, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        try
        {
            var now = clock.GetUtcNow();
            var success = attempt.EventType == RiskEventTypes.LoginSuccess;
            var prefix = IpPrefix.From(origin.RemoteIpAddress);
            var localHour = OddHoursRule.LocalHour(now, zone.Value);
            var history = await store.GetHistoryAsync(user.Id, prefix, cancellationToken);
            var reuse = await store.HasRecentRefreshTokenReuseAsync(user.Id, cancellationToken);
            var mfaFailures = attempt.FailedMfaAttempts ?? await new MfaAttemptGuard(redis).GetFailureCountAsync(user.Id);

            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var passwordFailures = attempt.FailedPasswordAttempts ?? await db.Users.AsNoTracking()
                .Where(item => item.Id == user.Id)
                .Select(item => item.FailedLoginAttempts)
                .FirstOrDefaultAsync(cancellationToken);

            var signals = new RiskSignals
            {
                FailedPasswordAttempts = passwordFailures,
                FailedMfaAttempts = mfaFailures,
                UnfamiliarDevice = attempt.UnfamiliarDevice,
                NewIpPrefix = history.IsNewPrefix(prefix),
                OddHours = OddHoursRule.IsOdd(localHour, history.HourHistogram, settings),
                RefreshTokenReuse = reuse,
                RecentLockout = user.LockoutEnd is { } lockoutEnd &&
                    lockoutEnd > now.UtcDateTime.AddHours(-Math.Max(0, settings.RecentLockoutWindowHours)),
                // Device posture and host-security signals are not available on the browser
                // sign-in path in R0; the scorer supports them for the session rescoring step.
                PasskeyUsed = success && attempt.Methods.Contains("passkey", StringComparer.OrdinalIgnoreCase),
            };
            var assessment = scorer.Score(signals);
            var methods = attempt.Methods.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
            var userAgent = origin.UserAgent;

            var severity = !success ? "warning" : assessment.Band == RiskBand.High ? "warning" : "info";
            SecurityEventWriter.Stage(db, user.Id, user.UserName, attempt.EventType, severity, prefix, userAgent, null,
                RiskEventDetails.From(assessment, prefix, methods, attempt.Outcome).ToJson(), assessment.Score, assessment.BandName);

            string? wouldAct = null;
            if (success && assessment.Band != RiskBand.Low)
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var isAdmin = await userManager.IsInRoleAsync(user, AdminRole);
                wouldAct = RiskPolicyPreview.WouldAct(assessment.Band, isAdmin);
                if (wouldAct is not null)
                    SecurityEventWriter.Stage(db, user.Id, user.UserName, wouldAct, "warning", prefix, userAgent, null,
                        RiskEventDetails.From(assessment, prefix, methods, "report_only").ToJson(), assessment.Score, assessment.BandName);
            }
            await db.SaveChangesAsync(cancellationToken);

            // Learn only from successful sign-ins, after scoring against the previous history.
            if (success)
                await store.RecordSuccessAsync(user.Id, prefix, localHour, cancellationToken);

            logger.LogInformation(
                "Sign-in risk {EventType} for {UserId}: score {Score} ({Band}) reasons {Reasons}{WouldAct}",
                attempt.EventType, user.Id, assessment.Score, assessment.BandName,
                string.Join(',', assessment.Reasons.Select(reason => $"{reason.Code}{reason.Points:+#;-#;0}")),
                wouldAct is null ? "" : $"; report-only policy: {wouldAct}");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Sign-in risk scoring failed for {EventType} of {UserId}; sign-in continues unchanged.",
                attempt.EventType, user.Id);
        }
    }

    public async Task RememberPendingPrimaryAsync(string pendingId, int failedPasswordAttempts, TimeSpan lifetime)
    {
        if (!options.Value.Enabled || failedPasswordAttempts <= 0) return;
        try
        {
            await store.RememberPendingPrimaryAsync(pendingId, failedPasswordAttempts, lifetime);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not keep the primary sign-in risk context; sign-in continues unchanged.");
        }
    }

    public async Task<int> TakePendingPrimaryAsync(string pendingId)
    {
        if (!options.Value.Enabled) return 0;
        try
        {
            return await store.TakePendingPrimaryAsync(pendingId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read the primary sign-in risk context; sign-in continues unchanged.");
            return 0;
        }
    }
}
