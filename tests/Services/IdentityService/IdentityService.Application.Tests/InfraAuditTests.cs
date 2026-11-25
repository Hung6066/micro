using FluentAssertions;
using His.Hope.IdentityService.Application.InfraAudit;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class InfraAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly InfraAuditOptions Options = new();

    [Fact]
    public void Query_uses_only_allowlisted_log_types_and_escapes_free_text()
    {
        InfraAuditQuery.TryCreateFilter(Now, Options, Now.AddHours(-1), Now, "vault,console", "a\"b\\c", "x\" } |= \"y", null, 50, out var filter)
            .Should().BeNull();

        var logQl = InfraAuditQuery.Build(filter);

        logQl.Should().StartWith("{job=\"openship-access-audit\", log_type=~\"console_audit|vault_audit\"}");
        // Quotes and backslashes cannot close the literal and append new pipeline stages.
        logQl.Should().Contain(" |= \"a\\\"b\\\\c\"");
        logQl.Should().EndWith(" |= \"x\\\" } |= \\\"y\"");
    }

    [Fact]
    public void Query_without_sources_covers_every_known_log_type()
    {
        InfraAuditQuery.TryCreateFilter(Now, Options, null, null, null, null, null, null, null, out var filter).Should().BeNull();
        var logQl = InfraAuditQuery.Build(filter);
        foreach (var logType in InfraAuditQuery.SourceLogTypes.Values.SelectMany(types => types))
            logQl.Should().Contain(logType);
        filter.To.Should().Be(Now);
        filter.From.Should().Be(Now.AddHours(-24));
    }

    [Theory]
    [InlineData("vault,unknown", null, null, null, "unknown_source")]
    [InlineData(null, "bad\nactor", null, null, "invalid_text")]
    [InlineData(null, null, "maybe", null, "unknown_result")]
    [InlineData(null, null, null, 0, "invalid_limit")]
    [InlineData(null, null, null, 501, "invalid_limit")]
    public void Invalid_parameters_are_rejected(string? sources, string? actor, string? result, int? limit, string expected) =>
        InfraAuditQuery.TryCreateFilter(Now, Options, null, null, sources, actor, null, result, limit, out _).Should().Be(expected);

    [Fact]
    public void Ranges_are_bounded_by_the_window_and_retention()
    {
        InfraAuditQuery.TryCreateFilter(Now, Options, Now, Now.AddHours(-1), null, null, null, null, null, out _).Should().Be("invalid_range");
        InfraAuditQuery.TryCreateFilter(Now, Options, Now.AddDays(-40), Now, null, null, null, null, null, out _).Should().Be("range_too_long");
        InfraAuditQuery.TryCreateFilter(Now, Options, Now.AddDays(-95), Now.AddDays(-91), null, null, null, null, null, out _).Should().Be("beyond_retention");
        InfraAuditQuery.TryCreateFilter(Now, Options, Now.AddDays(-30), Now, null, null, null, null, null, out _).Should().BeNull();
    }

    [Fact]
    public void Actor_alternatives_become_a_literal_case_insensitive_regex()
    {
        var filter = new InfraAuditFilter(Now.AddHours(-1), Now, [], null, null, null, 10, ["ops.user@example.com", "id(1)"]);
        InfraAuditQuery.Build(filter).Should().EndWith(" |~ \"(?i)(ops\\\\.user@example\\\\.com|id\\\\(1\\\\))\"");
    }

    [Fact]
    public void Console_vault_and_identity_lines_are_normalized()
    {
        var console = InfraAuditNormalizer.Normalize(Now,
            """{"log_type":"console_audit","console":"redis-rw","user":"ops@example.com","write":"1","method":"POST","uri":"/api/keys","status":403,"remote_addr":"127.0.0.1","request_id":"r1","request_time":0.2}""")!;
        console.Source.Should().Be("console");
        console.Actor.Should().Be("ops@example.com");
        console.Action.Should().Be("POST redis-rw (write)");
        console.Target.Should().Be("redis-rw:/api/keys");
        console.Result.Should().Be(InfraAuditResults.Denied);
        console.CorrelationId.Should().Be("r1");

        var vault = InfraAuditNormalizer.Normalize(Now,
            """{"log_type":"vault_audit","path":"ssh-client-signer/sign/root","operation":"update","display_name":"oidc-ops@example.com","remote_address":"10.42.1.5","error":"1 error occurred: permission denied"}""")!;
        vault.Actor.Should().Be("ops@example.com");
        vault.Result.Should().Be(InfraAuditResults.Denied);
        vault.Action.Should().Be("update ssh-client-signer/sign/root");

        var identity = InfraAuditNormalizer.Normalize(Now,
            """{"log_type":"identity_security","template":"Login failed","user_id":"u-1","security_event":"identity_auth_failure","client_ip":"1.2.3.4"}""")!;
        identity.Source.Should().Be("identity");
        identity.Result.Should().Be(InfraAuditResults.Denied);
        identity.Actor.Should().Be("u-1");
    }

    [Fact]
    public void Unknown_log_types_still_surface_and_garbage_is_skipped()
    {
        InfraAuditNormalizer.Normalize(Now, """{"log_type":"kube_audit","component":"kube-apiserver","user":"oidc:a@b","action":"delete pods","status":200}""")!
            .Should().Match<InfraAuditEvent>(item => item.Source == "kube-apiserver" && item.Result == InfraAuditResults.Allowed && item.Actor == "oidc:a@b");
        InfraAuditNormalizer.Normalize(Now, "not json").Should().BeNull();
        InfraAuditNormalizer.Normalize(Now, "[1,2]").Should().BeNull();
    }

    [Theory]
    [InlineData("connect postgres://app:S3cret@db:5432/x failed", "connect postgres://***@db:5432/x failed")]
    [InlineData("password=hunter2 next", "password=*** next")]
    [InlineData("client_secret: abc123def", "client_secret=***")]
    [InlineData("Authorization Bearer eyJhbGciOiJSUzI1NiJ9.x.y", "Authorization Bearer ***")]
    public void Secrets_in_free_text_are_masked(string input, string expected) =>
        InfraAuditNormalizer.Mask(input).Should().Be(expected);

    [Fact]
    public async Task Pages_filter_by_result_and_actor_and_continue_with_older_events()
    {
        var reader = new FakeReader(
            (Now.AddMinutes(-1), """{"log_type":"console_audit","console":"vault","user":"a@example.com","status":200}"""),
            (Now.AddMinutes(-2), """{"log_type":"console_audit","console":"vault","user":"b@example.com","status":403}"""),
            (Now.AddMinutes(-3), """{"log_type":"console_audit","console":"vault","user":"a@example.com","status":403}"""));
        var service = new InfraAuditService(reader, Microsoft.Extensions.Options.Options.Create(Options));
        var filter = new InfraAuditFilter(Now.AddHours(-1), Now, [], "a@example.com", null, InfraAuditResults.Denied, 3);

        var page = await service.GetPageAsync(filter, CancellationToken.None);

        page.Items.Should().ContainSingle().Which.Timestamp.Should().Be(Now.AddMinutes(-3));
        page.NextTo.Should().Be(Now.AddMinutes(-3).AddTicks(-1));
        reader.LastQuery.Should().Contain(" |= \"a@example.com\"");
    }

    [Fact]
    public async Task My_activity_requires_an_exact_actor_match()
    {
        var reader = new FakeReader(
            (Now.AddMinutes(-1), """{"log_type":"console_audit","console":"vault","user":"me@example.com","status":200}"""),
            (Now.AddMinutes(-2), """{"log_type":"console_audit","console":"vault","user":"notme@example.com","status":200}"""));
        var service = new InfraAuditService(reader, Microsoft.Extensions.Options.Options.Create(Options));
        var filter = new InfraAuditFilter(Now.AddHours(-1), Now, [], null, null, null, 50, ["me@example.com", "user-id"]);

        var page = await service.GetPageAsync(filter, CancellationToken.None);

        page.Items.Should().ContainSingle().Which.Actor.Should().Be("me@example.com");
        page.NextTo.Should().BeNull();
    }

    [Fact]
    public async Task Summary_counts_by_source_result_actor_and_hour()
    {
        var reader = new FakeReader(
            (Now.AddMinutes(-1), """{"log_type":"console_audit","console":"vault","user":"a@example.com","status":200}"""),
            (Now.AddMinutes(-70), """{"log_type":"vault_audit","path":"sys/health","operation":"read","display_name":"oidc-a@example.com"}"""));
        var service = new InfraAuditService(reader, Microsoft.Extensions.Options.Options.Create(Options));

        var summary = await service.SummarizeAsync(new InfraAuditFilter(Now.AddHours(-3), Now, [], null, null, null, 100), CancellationToken.None);

        summary.Total.Should().Be(2);
        summary.BySource.Should().Contain("console", 1).And.Contain("vault", 1);
        summary.TopActors.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, int>("a@example.com", 2));
        summary.PerHour.Should().HaveCount(2);
    }

    private sealed class FakeReader(params (DateTimeOffset Timestamp, string Line)[] lines) : IInfraAuditLogReader
    {
        public string? LastQuery { get; private set; }
        public bool IsConfigured => true;

        public Task<IReadOnlyList<(DateTimeOffset Timestamp, string Line)>> ReadAsync(
            string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
        {
            LastQuery = logQl;
            IReadOnlyList<(DateTimeOffset, string)> result = lines
                .Where(line => line.Timestamp >= from && line.Timestamp <= to)
                .OrderByDescending(line => line.Timestamp).Take(limit).ToList();
            return Task.FromResult(result);
        }
    }
}
