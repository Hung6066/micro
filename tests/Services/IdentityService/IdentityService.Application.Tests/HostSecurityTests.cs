using FluentAssertions;
using His.Hope.IdentityService.Application.HostSecurity;
using His.Hope.IdentityService.Application.InfraAudit;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class HostSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, string> AccessLabels = new() { ["job"] = "openship-access-audit" };

    private const string Drift =
        """{"timestamp":"2026-11-27T08:18:00Z","component":"host-security","log_type":"host_integrity","host":"vmi3129107","security_event":"integrity_drift","severity":"critical","drift_count":2,"fingerprint":"3dec450c11effb93","new_drift":true,"changes":[{"category":"sudoers","path":"/etc/sudoers.d/zz","change":"added","severity":"critical","sha256":"73fb"},{"category":"listeners","path":"tcp 0.0.0.0:4444 nc","change":"added","severity":"warning"}]}""";

    private const string Ok =
        """{"log_type":"host_integrity","host":"vmi3129107","security_event":"integrity_ok","severity":"info","drift_count":0}""";

    private const string SudoFailure =
        """{"log_type":"sudo","host":"vmi3129107","user":"ops","security_event":"sudo_failure","severity":"warning","failure":"user NOT in sudoers","command":"/usr/bin/psql postgres://app:hunter2@db/x","target_user":"root","tty":"pts/0"}""";

    private const string RootLogin =
        """{"log_type":"sshd_auth","host":"vmi3600038","user":"agent-root grant=g device=d","action":"ssh login as root (publickey) cert serial 42","source_ip":"10.241.0.1","security_event":"ssh_root_login","severity":"info"}""";

    private static LokiLine Line(string line, int minutesAgo = 1, Dictionary<string, string>? labels = null) =>
        new(Now.AddMinutes(-minutesAgo), line, labels ?? AccessLabels);

    private static LokiLine FalcoLine(string priority, string rule, string text, int minutesAgo = 1) =>
        new(Now.AddMinutes(-minutesAgo), text, new Dictionary<string, string>
        {
            ["priority"] = priority, ["rule"] = rule, ["hostname"] = "vmi3600039",
            ["k8s_ns_name"] = "openship-apps", ["k8s_pod_name"] = "api-1", ["source"] = "syscall",
        });

    [Fact]
    public void Drift_keeps_changes_and_acks_by_fingerprint()
    {
        var item = HostSecurityNormalizer.Normalize(Line(Drift))!;
        item.Category.Should().Be(HostSecurityCategories.Integrity);
        item.Severity.Should().Be("critical");
        item.Host.Should().Be("vmi3129107");
        item.AckKey.Should().Be("drift:vmi3129107:3dec450c11effb93");
        item.Changes.Should().HaveCount(2);
        item.Summary.Should().StartWith("2 unapproved changes: added /etc/sudoers.d/zz");
        // Every 5-minute repeat of the same drift is covered by one acknowledgement.
        HostSecurityNormalizer.Normalize(Line(Drift, 6))!.AckKey.Should().Be(item.AckKey);
    }

    [Fact]
    public void Heartbeats_and_unknown_lines_are_dropped()
    {
        HostSecurityNormalizer.Normalize(Line(Ok)).Should().BeNull();
        HostSecurityNormalizer.Normalize(Line("not json")).Should().BeNull();
        HostSecurityNormalizer.Normalize(Line("""{"log_type":"vault_audit","security_event":"x"}""")).Should().BeNull();
    }

    [Fact]
    public void Sudo_failure_masks_credentials()
    {
        var item = HostSecurityNormalizer.Normalize(Line(SudoFailure))!;
        item.Category.Should().Be(HostSecurityCategories.Sudo);
        item.Actor.Should().Be("ops");
        item.Summary.Should().Contain("sudo refused (user NOT in sudoers)");
        item.Summary.Should().NotContain("hunter2");
        item.AckKey.Should().MatchRegex("^evt:[0-9a-f]{20}$");
    }

    [Fact]
    public void Login_uses_the_certificate_identity()
    {
        var item = HostSecurityNormalizer.Normalize(Line(RootLogin))!;
        item.Category.Should().Be(HostSecurityCategories.Login);
        item.Actor.Should().Be("agent-root grant=g device=d");
        item.Summary.Should().Be("ssh login as root (publickey) cert serial 42 from 10.241.0.1");
    }

    [Theory]
    [InlineData("Critical", "critical")]
    [InlineData("Warning", "warning")]
    [InlineData("Error", "warning")]
    [InlineData("Notice", "info")]
    public void Falco_priority_maps_to_severity(string priority, string severity)
    {
        var item = HostSecurityNormalizer.Normalize(FalcoLine(priority, "Terminal shell in container",
            $"08:41:49.145969500: {priority} A shell was spawned in a container | user=root command=sh -c env TOKEN=abc123"))!;
        item.Category.Should().Be(HostSecurityCategories.Runtime);
        item.SecurityEvent.Should().Be("Terminal shell in container");
        item.Severity.Should().Be(severity);
        item.Host.Should().Be("vmi3600039");
        item.Actor.Should().Be("openship-apps/api-1");
        item.Summary.Should().Be("A shell was spawned in a container");
        item.Detail.Should().NotContain("abc123");
    }

    [Theory]
    [InlineData("unknown", null, null, null, "unknown_category")]
    [InlineData(null, "loud", null, null, "unknown_severity")]
    [InlineData(null, null, "bad host!", null, "invalid_host")]
    [InlineData(null, null, null, 0, "invalid_limit")]
    [InlineData(null, null, null, 501, "invalid_limit")]
    public void Invalid_filters_are_rejected(string? categories, string? severity, string? host, int? limit, string expected)
    {
        var (service, _, _) = Create();
        service.TryCreateFilter(null, null, categories, severity, host, null, null, limit, out _).Should().Be(expected);
    }

    [Fact]
    public async Task Page_merges_sources_filters_severity_and_attaches_acks()
    {
        var (service, reader, acks) = Create();
        reader.Responses["openship-access-audit"] = [Line(Drift, 1), Line(Ok, 2), Line(SudoFailure, 3), Line(RootLogin, 4), Line(Drift, 6)];
        reader.Responses["openship-runtime-security"] = [FalcoLine("Critical", "Drop and execute new binary in container", "08:00:00: Critical Executing binary not part of base image | proc=/tmp/x", 5)];
        acks.Stored["drift:vmi3129107:3dec450c11effb93"] = new HostSecurityAck("drift:vmi3129107:3dec450c11effb93", "alice", Now, "planned");

        service.TryCreateFilter(null, null, null, "warning", null, null, null, 50, out var filter).Should().BeNull();
        var page = await service.GetPageAsync(filter, CancellationToken.None);

        page.Items.Select(item => item.SecurityEvent).Should().Equal(
            "integrity_drift", "sudo_failure", "Drop and execute new binary in container", "integrity_drift");
        page.Items.Where(item => item.SecurityEvent == "integrity_drift").Should().OnlyContain(item => item.Ack!.AckedBy == "alice");
        page.UnacknowledgedBySeverity.Should().BeEquivalentTo(new Dictionary<string, int> { ["warning"] = 1, ["critical"] = 1 });
        page.UnavailableSources.Should().BeEmpty();
        reader.Queries["openship-access-audit"].Should().Contain("sev=~\"warning|critical\"");
        reader.Queries["openship-runtime-security"].Should().Contain("priority=~\"Warning|Error|Critical|Alert|Emergency\"");

        service.TryCreateFilter(null, null, null, "warning", null, null, true, 50, out var open).Should().BeNull();
        (await service.GetPageAsync(open, CancellationToken.None)).Items.Should().NotContain(item => item.Ack != null);
    }

    [Fact]
    public async Task One_failing_source_is_reported_and_the_other_still_answers()
    {
        var (service, reader, _) = Create();
        reader.Responses["openship-access-audit"] = [Line(SudoFailure)];
        reader.Failing.Add("openship-runtime-security");
        service.TryCreateFilter(null, null, null, null, null, null, null, null, out var filter).Should().BeNull();
        var page = await service.GetPageAsync(filter, CancellationToken.None);
        page.Items.Should().ContainSingle();
        page.UnavailableSources.Should().Equal("runtime-security");
    }

    [Fact]
    public async Task Text_filter_cannot_break_out_of_the_line_filter()
    {
        var (service, reader, _) = Create();
        service.TryCreateFilter(null, null, "sudo", null, null, "a\" } |= \"b", null, null, out var filter).Should().BeNull();
        await service.GetPageAsync(filter, CancellationToken.None);
        // RE2 metacharacters are escaped, the quote is escaped for the LogQL string: still one line filter.
        reader.Queries["openship-access-audit"].Should().Contain(" |~ \"(?i)a\\\" \\\\} \\\\|= \\\"b\" | json ");
        HostSecurityService.EscapeRe2("a b.c").Should().Be("a b\\.c");
    }

    [Fact]
    public async Task Host_status_takes_the_latest_report_and_marks_stale_hosts()
    {
        var (service, reader, _) = Create();
        reader.Responses["openship-access-audit"] =
        [
            Line(Ok, 3),
            Line(Drift, 8),
            Line("""{"log_type":"host_integrity","host":"pg-53","security_event":"integrity_ok","severity":"info","drift_count":0}""", 45),
        ];
        var hosts = await service.GetHostStatusAsync(CancellationToken.None);
        hosts.Should().HaveCount(2);
        hosts.Single(item => item.Host == "vmi3129107").LastEvent.Should().Be("integrity_ok");
        hosts.Single(item => item.Host == "vmi3129107").Stale.Should().BeFalse();
        hosts.Single(item => item.Host == "pg-53").Stale.Should().BeTrue();
    }

    private static (HostSecurityService Service, FakeReader Reader, FakeAcks Acks) Create()
    {
        var reader = new FakeReader();
        var acks = new FakeAcks();
        var clock = new FixedClock(Now);
        return (new HostSecurityService(reader, acks, Microsoft.Extensions.Options.Options.Create(new InfraAuditOptions()), clock), reader, acks);
    }

    private sealed class FakeReader : ILokiStreamReader
    {
        public Dictionary<string, List<LokiLine>> Responses { get; } = new();
        public Dictionary<string, string> Queries { get; } = new();
        public HashSet<string> Failing { get; } = [];
        public bool IsConfigured => true;

        public Task<IReadOnlyList<LokiLine>> ReadStreamsAsync(string tenant, string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
        {
            Queries[tenant] = logQl;
            if (Failing.Contains(tenant)) throw new LokiUnavailableException("upstream_502");
            IReadOnlyList<LokiLine> lines = Responses.TryGetValue(tenant, out var found)
                ? found.Where(line => line.Timestamp >= from && line.Timestamp <= to).ToList()
                : [];
            return Task.FromResult(lines);
        }
    }

    private sealed class FakeAcks : IHostSecurityAckStore
    {
        public Dictionary<string, HostSecurityAck> Stored { get; } = new();

        public Task<IReadOnlyDictionary<string, HostSecurityAck>> GetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, HostSecurityAck>>(
                Stored.Where(pair => keys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
