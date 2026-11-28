namespace His.Hope.IdentityService.Application.Risk;

/// <summary>
/// Zero Trust 3.5 sign-in risk weights (configuration section <c>RiskScoring</c>).
/// The defaults are the R0 start values from the design; the 7-day review tunes
/// them in configuration without a release. R0 is report-only: nothing here
/// blocks or changes a sign-in.
/// </summary>
public sealed class RiskScoringOptions
{
    public const string SectionName = "RiskScoring";

    /// <summary>Master switch for scoring and the risk events (fail open when off).</summary>
    public bool Enabled { get; set; } = true;

    public int FailedPasswordWeight { get; set; } = 10;
    public int FailedPasswordMax { get; set; } = 40;
    public int FailedMfaWeight { get; set; } = 15;
    public int FailedMfaMax { get; set; } = 45;
    public int UnfamiliarDeviceWeight { get; set; } = 20;
    public int NewIpPrefixWeight { get; set; } = 15;
    public int OddHoursWeight { get; set; } = 10;
    public int RefreshTokenReuseWeight { get; set; } = 60;
    public int RecentLockoutWeight { get; set; } = 20;
    public int StaleDevicePostureWeight { get; set; } = 20;
    public int HostSecuritySignalWeight { get; set; } = 40;
    /// <summary>Subtracted when the sign-in used a passkey; the score never drops below 0.</summary>
    public int PasskeyReduction { get; set; } = 20;

    /// <summary>Scores at or above this are "medium" (below is "low").</summary>
    public int MediumThreshold { get; set; } = 30;
    /// <summary>Scores at or above this are "high".</summary>
    public int HighThreshold { get; set; } = 60;
    public int MaxScore { get; set; } = 100;

    /// <summary>How long IP prefixes and the sign-in hour histogram are kept per user.</summary>
    public int HistoryDays { get; set; } = 30;
    /// <summary>A user's own hours replace the fixed night window once this many sign-ins were seen.</summary>
    public int LearnedHoursMinSignIns { get; set; } = 10;
    /// <summary>Fixed odd-hours window before learning: from this hour (inclusive) ...</summary>
    public int OddHoursStartHour { get; set; } = 22;
    /// <summary>... to this hour (exclusive), in <see cref="TimeZone"/>.</summary>
    public int OddHoursEndHour { get; set; } = 5;
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    /// <summary>How long a refresh-token reuse keeps counting against the user.</summary>
    public int RefreshTokenReuseWindowHours { get; set; } = 24;
    /// <summary>A lockout that ended within this window counts as "recently locked out".</summary>
    public int RecentLockoutWindowHours { get; set; } = 24;
}
