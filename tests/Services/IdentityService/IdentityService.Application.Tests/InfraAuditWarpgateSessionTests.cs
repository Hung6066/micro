using FluentAssertions;
using His.Hope.IdentityService.Application.InfraAudit;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class InfraAuditWarpgateSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private const string SessionA = "6f1c2a3b-0000-4000-8000-00000000000a";
    private const string SessionB = "6f1c2a3b-0000-4000-8000-00000000000b";
    private const string SessionC = "6f1c2a3b-0000-4000-8000-00000000000c";

    [Fact]
    public void Query_is_the_allowlisted_warpgate_selector_with_a_constant_lifecycle_filter()
    {
        var logQl = WarpgateSessionTracker.BuildQuery(Now.AddHours(-24), Now, 5000);

        logQl.Should().Be(
            "{job=\"openship-access-audit\", log_type=~\"warpgate_event\"} |~ \"(?i)(started|ended|closed|finished|terminated|disconnected)\"");
    }

    [Theory]
    [InlineData(null, 24, null)]
    [InlineData(1, 1, null)]
    [InlineData(744, 744, null)]
    [InlineData(0, 0, "invalid_range")]
    [InlineData(-5, 0, "invalid_range")]
    [InlineData(745, 0, "range_too_long")]
    public void Window_is_bounded_by_the_longest_query(int? hours, int expectedHours, string? expectedError)
    {
        var error = WarpgateSessionTracker.TryCreateWindow(Now, new InfraAuditOptions(), hours, out var from, out var to);

        error.Should().Be(expectedError);
        if (expectedError is null)
        {
            to.Should().Be(Now);
            from.Should().Be(Now.AddHours(-expectedHours));
        }
    }

    [Fact]
    public void Sessions_with_a_start_and_no_end_are_open_and_ended_ones_are_not()
    {
        var events = new[]
        {
            Line(Now.AddMinutes(-50), SessionA, "Session started", "ops@example.com", "k3s-manager", "100.64.0.7"),
            Line(Now.AddMinutes(-40), SessionA, "Shell command: kubectl get pods"),
            Line(Now.AddMinutes(-30), SessionB, "Session started", "dev@example.com", "worker-54", "100.64.0.9"),
            Line(Now.AddMinutes(-10), SessionB, "Session closed", "dev@example.com", "worker-54"),
            Line(Now.AddMinutes(-5), SessionC, "Session started", "late@example.com", "k3s-manager", "100.64.0.8"),
        };

        var open = WarpgateSessionTracker.OpenSessions(events);

        open.Select(session => session.SessionId).Should().Equal(SessionC, SessionA);
        var a = open.Single(session => session.SessionId == SessionA);
        a.User.Should().Be("ops@example.com");
        a.Target.Should().Be("k3s-manager");
        a.SourceIp.Should().Be("100.64.0.7");
        a.StartedAt.Should().Be(Now.AddMinutes(-50));
    }

    [Fact]
    public void Order_of_lines_does_not_matter_and_an_end_before_the_start_still_closes()
    {
        var open = WarpgateSessionTracker.OpenSessions(new[]
        {
            Line(Now.AddMinutes(-1), SessionA, "Session ended"),
            Line(Now.AddMinutes(-20), SessionA, "Session started", "ops@example.com", "k3s-manager"),
        });

        open.Should().BeEmpty();
    }

    [Fact]
    public void Shell_commands_mentioning_lifecycle_words_neither_start_nor_end_a_session()
    {
        var open = WarpgateSessionTracker.OpenSessions(new[]
        {
            Line(Now.AddMinutes(-20), SessionA, "Session started", "ops@example.com", "k3s-manager"),
            Line(Now.AddMinutes(-10), SessionA, "Shell command: systemctl status | grep closed"),
            Line(Now.AddMinutes(-9), SessionB, "Shell command: echo started"),
        });

        open.Should().ContainSingle().Which.SessionId.Should().Be(SessionA);
    }

    [Fact]
    public void Missing_fields_on_the_start_are_taken_from_other_lines_and_lines_without_an_id_are_ignored()
    {
        var open = WarpgateSessionTracker.OpenSessions(new[]
        {
            Line(Now.AddMinutes(-20), SessionA, "Session started"),
            Line(Now.AddMinutes(-15), SessionA, "Shell command: id", "ops@example.com", "k3s-manager", "100.64.0.7"),
            Line(Now.AddMinutes(-12), "", "Session started", "nobody@example.com", "k3s-manager"),
        });

        var session = open.Should().ContainSingle().Subject;
        session.User.Should().Be("ops@example.com");
        session.Target.Should().Be("k3s-manager");
        session.SourceIp.Should().Be("100.64.0.7");
        session.StartedAt.Should().Be(Now.AddMinutes(-20));
    }

    [Fact]
    public async Task Service_reads_with_the_lifecycle_query_and_reports_open_sessions()
    {
        var reader = new FakeReader(
        [
            (Now.AddMinutes(-30), Raw(SessionA, "Session started", "ops@example.com", "k3s-manager", "100.64.0.7")),
            (Now.AddMinutes(-20), Raw(SessionB, "Session started", "dev@example.com", "worker-54", "")),
            (Now.AddMinutes(-10), Raw(SessionB, "Session ended", "dev@example.com", "worker-54", "")),
            (Now.AddMinutes(-5), "not json"),
        ]);
        var service = new InfraAuditService(reader, Options.Create(new InfraAuditOptions()));

        var result = await service.GetOpenWarpgateSessionsAsync(Now.AddHours(-24), Now, CancellationToken.None);

        reader.LastQuery.Should().Be(WarpgateSessionTracker.BuildQuery(Now.AddHours(-24), Now, 5000));
        result.Truncated.Should().BeFalse();
        var session = result.Items.Should().ContainSingle().Subject;
        session.Should().Be(new WarpgateOpenSession(SessionA, "ops@example.com", "k3s-manager", Now.AddMinutes(-30), "100.64.0.7"));
    }

    /// <summary>A line shaped like the access-audit Vector warpgate_event output, normalized.</summary>
    private static InfraAuditEvent Line(DateTimeOffset at, string sessionId, string action,
        string user = "", string target = "", string ip = "") =>
        InfraAuditNormalizer.Normalize(at, Raw(sessionId, action, user, target, ip))!;

    private static string Raw(string sessionId, string action, string user, string target, string ip) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["component"] = "warpgate",
            ["log_type"] = "warpgate_event",
            ["host"] = "vmi3600038",
            ["user"] = user,
            ["action"] = action,
            ["target"] = target,
            ["result"] = action.Contains("started", StringComparison.Ordinal) ? "allowed" : "",
            ["level"] = "INFO",
            ["source_ip"] = ip,
            ["correlation_id"] = sessionId,
        });

    private sealed class FakeReader(IReadOnlyList<(DateTimeOffset Timestamp, string Line)> lines) : IInfraAuditLogReader
    {
        public string? LastQuery { get; private set; }

        public bool IsConfigured => true;

        public Task<IReadOnlyList<(DateTimeOffset Timestamp, string Line)>> ReadAsync(
            string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
        {
            LastQuery = logQl;
            return Task.FromResult(lines);
        }
    }
}
