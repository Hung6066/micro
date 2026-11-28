using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace His.Hope.IdentityService.Application.Risk;

/// <summary>SecurityEvent types written by the sign-in risk hook (Zero Trust 3.5).</summary>
public static class RiskEventTypes
{
    public const string LoginSuccess = "login_success";
    public const string LoginFailed = "login_failed";
    public const string MfaFailed = "mfa_failed";
    public const string WouldStepUp = "risk_policy_would_step_up";
    public const string WouldDeny = "risk_policy_would_deny";

    public static readonly IReadOnlyList<string> Scored = [LoginSuccess, LoginFailed, MfaFailed];
    public static readonly IReadOnlyList<string> WouldAct = [WouldStepUp, WouldDeny];
}

/// <summary>Redis keys of the risk signals. Only prefixes and hour counts are stored, never raw IPs.</summary>
public static class RiskSignalKeys
{
    public static string IpPrefixes(Guid userId) => $"hishop:identity:risk:ip-prefixes:{userId:N}";
    public static string SignInHours(Guid userId) => $"hishop:identity:risk:hours:{userId:N}";
    public static string RefreshTokenReuse(Guid userId) => $"hishop:identity:risk:refresh-reuse:{userId:N}";
    public static string PendingPrimary(string pendingId) => $"hishop:identity:risk:pending:{pendingId}";
}

public static class IpPrefix
{
    /// <summary>The /24 (IPv4) or /48 (IPv6) network of an address, e.g. "203.0.113.0/24"; null when unknown.</summary>
    public static string? From(IPAddress? address)
    {
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return $"{new IPAddress(bytes)}/24";
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            for (var index = 6; index < bytes.Length; index++) bytes[index] = 0;
            return $"{new IPAddress(bytes)}/48";
        }
        return null;
    }

    public static string? From(string? address) =>
        IPAddress.TryParse(address, out var parsed) ? From(parsed) : null;
}

public static class OddHoursRule
{
    /// <summary>
    /// Before the user has <see cref="RiskScoringOptions.LearnedHoursMinSignIns"/> sign-ins the fixed
    /// window (22:00-05:00 local) is odd. After that an hour is odd when neither it nor an adjacent hour
    /// has a previous sign-in (the one-hour tolerance keeps the false-positive rate down).
    /// </summary>
    public static bool IsOdd(int localHour, IReadOnlyDictionary<int, long> histogram, RiskScoringOptions options)
    {
        if (localHour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(localHour));
        var total = histogram.Values.Sum();
        if (total < options.LearnedHoursMinSignIns)
            return InFixedWindow(localHour, options.OddHoursStartHour, options.OddHoursEndHour);

        long Count(int hour) => histogram.TryGetValue((hour + 24) % 24, out var value) ? value : 0;
        return Count(localHour) + Count(localHour - 1) + Count(localHour + 1) == 0;
    }

    public static bool InFixedWindow(int hour, int start, int end) =>
        start == end ? false
        : start < end ? hour >= start && hour < end
        : hour >= start || hour < end;

    public static int LocalHour(DateTimeOffset now, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(now, zone).Hour;

    /// <summary>Resolves the configured zone; falls back to UTC+7 (no DST) when the host has no tz data.</summary>
    public static TimeZoneInfo ResolveZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }
}

/// <summary>
/// R0 preview of the R1 policy: on an admin account, medium would step up and high would deny.
/// Nothing is enforced; the caller only logs the returned event type.
/// </summary>
public static class RiskPolicyPreview
{
    public static string? WouldAct(RiskBand band, bool isAdmin) =>
        !isAdmin ? null
        : band switch
        {
            RiskBand.High => RiskEventTypes.WouldDeny,
            RiskBand.Medium => RiskEventTypes.WouldStepUp,
            _ => null,
        };
}

/// <summary>Shape of <c>SecurityEvent.Details</c> for the risk events.</summary>
public sealed record RiskEventDetails(
    RiskEventRisk Risk,
    string? IpPrefix,
    IReadOnlyList<string>? Methods,
    string? Outcome = null)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Exact substring of the serialized details for a band (the admin filter relies on it).</summary>
    public static string BandMarker(RiskBand band) => $"\"band\":\"{RiskBands.Name(band)}\"";

    public static RiskEventDetails From(RiskAssessment assessment, string? ipPrefix, IReadOnlyList<string>? methods, string? outcome = null) =>
        new(new RiskEventRisk(assessment.Score, assessment.BandName,
                assessment.Reasons.Select(reason => new RiskEventReason(reason.Code, reason.Points)).ToArray()),
            ipPrefix, methods, outcome);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static RiskEventDetails? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var details = JsonSerializer.Deserialize<RiskEventDetails>(json, Json);
            return details?.Risk is null ? null : details;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record RiskEventRisk(int Score, string Band, IReadOnlyList<RiskEventReason> Reasons);

public sealed record RiskEventReason(string Code, int Points);

/// <summary>What the signal store knows about a user's earlier sign-ins.</summary>
public sealed record SignInRiskHistory(bool HasPrefixBaseline, bool KnownPrefix, IReadOnlyDictionary<int, long> HourHistogram)
{
    public static readonly SignInRiskHistory Empty = new(false, false, new Dictionary<int, long>());

    /// <summary>A prefix is "new" only against an existing baseline, so the first sign-in after rollout is not flagged.</summary>
    public bool IsNewPrefix(string? prefix) => prefix is not null && HasPrefixBaseline && !KnownPrefix;
}

/// <summary>
/// Per-user sign-in history for the risk signals (Redis in production). It stores IP prefixes
/// (/24, /48) and an hour histogram with a 30-day horizon, never raw IP history.
/// </summary>
public interface ISignInRiskSignalStore
{
    Task<SignInRiskHistory> GetHistoryAsync(Guid userId, string? ipPrefix, CancellationToken cancellationToken = default);

    /// <summary>Learns from a successful sign-in only, so failed attempts cannot teach a prefix or hour.</summary>
    Task RecordSuccessAsync(Guid userId, string? ipPrefix, int localHour, CancellationToken cancellationToken = default);

    Task<bool> HasRecentRefreshTokenReuseAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Keeps the password-failure count of a primary sign-in until its MFA step completes.</summary>
    Task RememberPendingPrimaryAsync(string pendingId, int failedPasswordAttempts, TimeSpan lifetime, CancellationToken cancellationToken = default);

    Task<int> TakePendingPrimaryAsync(string pendingId, CancellationToken cancellationToken = default);
}
