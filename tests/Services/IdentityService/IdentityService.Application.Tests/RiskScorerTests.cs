using System.Net;
using FluentAssertions;
using His.Hope.IdentityService.Application.Risk;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class RiskScorerTests
{
    private static readonly RiskScoringOptions Defaults = new();
    private static RiskAssessment Score(RiskSignals signals, RiskScoringOptions? options = null) =>
        new RiskScorer(options ?? Defaults).Score(signals);

    [Fact]
    public void No_signals_is_low_with_no_reasons()
    {
        var result = Score(new RiskSignals());
        result.Score.Should().Be(0);
        result.Band.Should().Be(RiskBand.Low);
        result.BandName.Should().Be("low");
        result.Reasons.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(3, 30)]
    [InlineData(4, 40)]
    [InlineData(9, 40)]
    public void Failed_passwords_add_ten_each_capped_at_forty(int attempts, int expected)
    {
        var result = Score(new RiskSignals { FailedPasswordAttempts = attempts });
        result.Score.Should().Be(expected);
        result.Reasons.Should().ContainSingle().Which.Should().Be(new RiskReason(RiskReasonCodes.FailedPassword, expected));
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 45)]
    [InlineData(5, 45)]
    public void Failed_mfa_adds_fifteen_each_capped_at_forty_five(int attempts, int expected)
    {
        var result = Score(new RiskSignals { FailedMfaAttempts = attempts });
        result.Score.Should().Be(expected);
        result.Reasons.Should().ContainSingle().Which.Code.Should().Be(RiskReasonCodes.FailedMfa);
    }

    public static TheoryData<RiskSignals, string, int> SingleSignals => new()
    {
        { new RiskSignals { UnfamiliarDevice = true }, RiskReasonCodes.UnfamiliarDevice, 20 },
        { new RiskSignals { NewIpPrefix = true }, RiskReasonCodes.NewIpPrefix, 15 },
        { new RiskSignals { OddHours = true }, RiskReasonCodes.OddHours, 10 },
        { new RiskSignals { RefreshTokenReuse = true }, RiskReasonCodes.RefreshTokenReuse, 60 },
        { new RiskSignals { RecentLockout = true }, RiskReasonCodes.RecentLockout, 20 },
        { new RiskSignals { StaleDevicePosture = true }, RiskReasonCodes.StaleDevicePosture, 20 },
        { new RiskSignals { HostSecuritySignal = true }, RiskReasonCodes.HostSecuritySignal, 40 },
    };

    [Theory]
    [MemberData(nameof(SingleSignals))]
    public void Each_signal_adds_its_default_weight(RiskSignals signals, string code, int points)
    {
        var result = Score(signals);
        result.Score.Should().Be(points);
        result.Reasons.Should().Equal(new RiskReason(code, points));
    }

    [Fact]
    public void Refresh_token_reuse_alone_is_high()
    {
        Score(new RiskSignals { RefreshTokenReuse = true }).Band.Should().Be(RiskBand.High);
    }

    [Fact]
    public void Passkey_subtracts_twenty()
    {
        var result = Score(new RiskSignals { UnfamiliarDevice = true, NewIpPrefix = true, PasskeyUsed = true });
        result.Score.Should().Be(15);
        result.Reasons.Should().Contain(new RiskReason(RiskReasonCodes.Passkey, -20));
    }

    [Fact]
    public void Passkey_reduction_never_goes_below_zero()
    {
        var result = Score(new RiskSignals { OddHours = true, PasskeyUsed = true });
        result.Score.Should().Be(0);
        result.Reasons.Should().Contain(new RiskReason(RiskReasonCodes.Passkey, -10));

        var clean = Score(new RiskSignals { PasskeyUsed = true });
        clean.Score.Should().Be(0);
        clean.Reasons.Should().BeEmpty();
    }

    [Fact]
    public void Total_is_capped_at_max_score()
    {
        var result = Score(new RiskSignals
        {
            FailedPasswordAttempts = 10, FailedMfaAttempts = 10, UnfamiliarDevice = true, NewIpPrefix = true,
            OddHours = true, RefreshTokenReuse = true, RecentLockout = true, StaleDevicePosture = true, HostSecuritySignal = true,
        });
        result.Score.Should().Be(100);
        result.Band.Should().Be(RiskBand.High);
        result.Reasons.Should().HaveCount(9);
    }

    [Theory]
    [InlineData(0, RiskBand.Low)]
    [InlineData(29, RiskBand.Low)]
    [InlineData(30, RiskBand.Medium)]
    [InlineData(59, RiskBand.Medium)]
    [InlineData(60, RiskBand.High)]
    [InlineData(100, RiskBand.High)]
    public void Bands_follow_the_thresholds(int score, RiskBand band)
    {
        RiskScorer.BandFor(score, Defaults).Should().Be(band);
    }

    [Fact]
    public void Weights_and_thresholds_come_from_options()
    {
        var options = new RiskScoringOptions { NewIpPrefixWeight = 35, MediumThreshold = 20, HighThreshold = 35 };
        var result = Score(new RiskSignals { NewIpPrefix = true }, options);
        result.Score.Should().Be(35);
        result.Band.Should().Be(RiskBand.High);
    }

    [Theory]
    [InlineData("203.0.113.77", "203.0.113.0/24")]
    [InlineData("::ffff:10.1.2.3", "10.1.2.0/24")]
    [InlineData("2001:db8:abcd:12:1::5", "2001:db8:abcd::/48")]
    public void Ip_prefix_keeps_only_the_network(string address, string expected)
    {
        IpPrefix.From(IPAddress.Parse(address)).Should().Be(expected);
        IpPrefix.From(address).Should().Be(expected);
    }

    [Fact]
    public void Ip_prefix_is_null_for_missing_or_invalid_addresses()
    {
        IpPrefix.From((IPAddress?)null).Should().BeNull();
        IpPrefix.From("not-an-ip").Should().BeNull();
    }

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(12, false)]
    [InlineData(21, false)]
    public void Before_learning_the_night_window_is_odd(int hour, bool odd)
    {
        var histogram = new Dictionary<int, long> { [9] = 9 };
        OddHoursRule.IsOdd(hour, histogram, Defaults).Should().Be(odd);
    }

    [Fact]
    public void After_ten_sign_ins_the_users_own_hours_apply_with_one_hour_tolerance()
    {
        var histogram = new Dictionary<int, long> { [23] = 6, [9] = 4 };
        OddHoursRule.IsOdd(23, histogram, Defaults).Should().BeFalse("a night worker's usual hour is not odd");
        OddHoursRule.IsOdd(0, histogram, Defaults).Should().BeFalse("adjacent to a usual hour (wraps at midnight)");
        OddHoursRule.IsOdd(10, histogram, Defaults).Should().BeFalse();
        OddHoursRule.IsOdd(14, histogram, Defaults).Should().BeTrue("never seen near 14:00");
        OddHoursRule.IsOdd(3, histogram, Defaults).Should().BeTrue();
    }

    [Fact]
    public void Local_hour_uses_indochina_time()
    {
        var zone = OddHoursRule.ResolveZone("Asia/Ho_Chi_Minh");
        OddHoursRule.LocalHour(new DateTimeOffset(2026, 11, 28, 16, 30, 0, TimeSpan.Zero), zone).Should().Be(23);
        OddHoursRule.LocalHour(new DateTimeOffset(2026, 11, 28, 16, 30, 0, TimeSpan.Zero), OddHoursRule.ResolveZone("No/Such_Zone")).Should().Be(23);
    }

    [Theory]
    [InlineData(RiskBand.Low, true, null)]
    [InlineData(RiskBand.Medium, true, RiskEventTypes.WouldStepUp)]
    [InlineData(RiskBand.High, true, RiskEventTypes.WouldDeny)]
    [InlineData(RiskBand.High, false, null)]
    public void Report_only_policy_acts_on_admins_from_medium(RiskBand band, bool admin, string? expected)
    {
        RiskPolicyPreview.WouldAct(band, admin).Should().Be(expected);
    }

    [Fact]
    public void New_prefix_needs_a_baseline()
    {
        SignInRiskHistory.Empty.IsNewPrefix("203.0.113.0/24").Should().BeFalse();
        new SignInRiskHistory(true, false, new Dictionary<int, long>()).IsNewPrefix("203.0.113.0/24").Should().BeTrue();
        new SignInRiskHistory(true, true, new Dictionary<int, long>()).IsNewPrefix("203.0.113.0/24").Should().BeFalse();
        new SignInRiskHistory(true, false, new Dictionary<int, long>()).IsNewPrefix(null).Should().BeFalse();
    }

    [Fact]
    public void Details_round_trip_and_carry_the_band_marker()
    {
        var assessment = Score(new RiskSignals { UnfamiliarDevice = true, NewIpPrefix = true });
        var json = RiskEventDetails.From(assessment, "203.0.113.0/24", ["pwd", "otp"]).ToJson();
        json.Should().Contain(RiskEventDetails.BandMarker(RiskBand.Medium));
        json.Should().NotContain("outcome");
        var parsed = RiskEventDetails.TryParse(json)!;
        parsed.Risk.Score.Should().Be(35);
        parsed.Risk.Band.Should().Be("medium");
        parsed.Risk.Reasons.Select(reason => reason.Code).Should().Equal(RiskReasonCodes.UnfamiliarDevice, RiskReasonCodes.NewIpPrefix);
        parsed.IpPrefix.Should().Be("203.0.113.0/24");
        RiskEventDetails.TryParse("{\"other\":1}").Should().BeNull();
        RiskEventDetails.TryParse("not json").Should().BeNull();
    }
}
