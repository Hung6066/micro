using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.Risk;

public enum RiskBand
{
    Low,
    Medium,
    High,
}

/// <summary>Signals observed for one sign-in attempt. Counts are attempts, flags are yes/no.</summary>
public sealed record RiskSignals
{
    public int FailedPasswordAttempts { get; init; }
    public int FailedMfaAttempts { get; init; }
    public bool UnfamiliarDevice { get; init; }
    public bool NewIpPrefix { get; init; }
    public bool OddHours { get; init; }
    public bool RefreshTokenReuse { get; init; }
    public bool RecentLockout { get; init; }
    public bool StaleDevicePosture { get; init; }
    public bool HostSecuritySignal { get; init; }
    public bool PasskeyUsed { get; init; }
}

/// <summary>One contribution to the score; <paramref name="Points"/> is negative for a reduction.</summary>
public sealed record RiskReason(string Code, int Points);

public sealed record RiskAssessment(int Score, RiskBand Band, IReadOnlyList<RiskReason> Reasons)
{
    public string BandName => RiskBands.Name(Band);
}

public static class RiskBands
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public static readonly IReadOnlyList<string> All = [Low, Medium, High];

    public static string Name(RiskBand band) => band switch
    {
        RiskBand.High => High,
        RiskBand.Medium => Medium,
        _ => Low,
    };

    public static bool TryParse(string? value, out RiskBand band)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case Low: band = RiskBand.Low; return true;
            case Medium: band = RiskBand.Medium; return true;
            case High: band = RiskBand.High; return true;
            default: band = RiskBand.Low; return false;
        }
    }
}

/// <summary>Reason codes written to <c>Details.risk.reasons</c>.</summary>
public static class RiskReasonCodes
{
    public const string FailedPassword = "failed_password";
    public const string FailedMfa = "failed_mfa";
    public const string UnfamiliarDevice = "unfamiliar_device";
    public const string NewIpPrefix = "new_ip_prefix";
    public const string OddHours = "odd_hours";
    public const string RefreshTokenReuse = "refresh_token_reuse";
    public const string RecentLockout = "recent_lockout";
    public const string StaleDevicePosture = "stale_device_posture";
    public const string HostSecuritySignal = "host_security_signal";
    public const string Passkey = "passkey";
}

public interface IRiskScorer
{
    RiskAssessment Score(RiskSignals signals);
}

/// <summary>
/// Pure risk scorer (no I/O): adds the configured weight of every signal present,
/// caps the repeated-failure signals, subtracts the passkey reduction (floor 0),
/// caps the total and maps it to a band.
/// </summary>
public sealed class RiskScorer(IOptions<RiskScoringOptions> options) : IRiskScorer
{
    public RiskScorer(RiskScoringOptions options) : this(Options.Create(options)) { }

    public RiskAssessment Score(RiskSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var o = options.Value;
        var reasons = new List<RiskReason>();
        var score = 0;

        void Add(bool present, string code, int points)
        {
            if (!present || points <= 0) return;
            reasons.Add(new RiskReason(code, points));
            score += points;
        }

        Add(signals.FailedPasswordAttempts > 0, RiskReasonCodes.FailedPassword,
            Capped(signals.FailedPasswordAttempts, o.FailedPasswordWeight, o.FailedPasswordMax));
        Add(signals.FailedMfaAttempts > 0, RiskReasonCodes.FailedMfa,
            Capped(signals.FailedMfaAttempts, o.FailedMfaWeight, o.FailedMfaMax));
        Add(signals.UnfamiliarDevice, RiskReasonCodes.UnfamiliarDevice, o.UnfamiliarDeviceWeight);
        Add(signals.NewIpPrefix, RiskReasonCodes.NewIpPrefix, o.NewIpPrefixWeight);
        Add(signals.OddHours, RiskReasonCodes.OddHours, o.OddHoursWeight);
        Add(signals.RefreshTokenReuse, RiskReasonCodes.RefreshTokenReuse, o.RefreshTokenReuseWeight);
        Add(signals.RecentLockout, RiskReasonCodes.RecentLockout, o.RecentLockoutWeight);
        Add(signals.StaleDevicePosture, RiskReasonCodes.StaleDevicePosture, o.StaleDevicePostureWeight);
        Add(signals.HostSecuritySignal, RiskReasonCodes.HostSecuritySignal, o.HostSecuritySignalWeight);

        if (signals.PasskeyUsed && o.PasskeyReduction > 0 && score > 0)
        {
            var reduction = Math.Min(o.PasskeyReduction, score);
            reasons.Add(new RiskReason(RiskReasonCodes.Passkey, -reduction));
            score -= reduction;
        }

        score = Math.Clamp(score, 0, Math.Max(0, o.MaxScore));
        return new RiskAssessment(score, BandFor(score, o), reasons);
    }

    public static RiskBand BandFor(int score, RiskScoringOptions options) =>
        score >= options.HighThreshold ? RiskBand.High
        : score >= options.MediumThreshold ? RiskBand.Medium
        : RiskBand.Low;

    private static int Capped(int count, int weight, int max) =>
        (int)Math.Min((long)Math.Max(0, count) * Math.Max(0, weight), Math.Max(0, max));
}
