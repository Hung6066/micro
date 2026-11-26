using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.InfraAudit;

/// <summary>Pages, summaries and exports over <see cref="IInfraAuditLogReader"/>.</summary>
public sealed class InfraAuditService(IInfraAuditLogReader reader, IOptions<InfraAuditOptions> options)
{
    private const int SummaryScanLimit = 5_000;
    private readonly InfraAuditOptions _options = options.Value;

    public bool IsConfigured => reader.IsConfigured;

    public InfraAuditOptions Options => _options;

    /// <summary>
    /// One page, newest first. <see cref="InfraAuditPage.NextTo"/> continues with older events.
    /// The result filter is applied after normalization, so a page can hold fewer items than
    /// the limit while more exist; NextTo is set whenever the scan was not exhausted.
    /// </summary>
    public async Task<InfraAuditPage> GetPageAsync(InfraAuditFilter filter, CancellationToken cancellationToken)
    {
        var raw = await reader.ReadAsync(InfraAuditQuery.Build(filter), filter.From, filter.To, filter.Limit, cancellationToken);
        var items = Normalize(raw, filter).ToList();
        DateTimeOffset? next = raw.Count >= filter.Limit
            ? raw.Min(entry => entry.Timestamp).AddTicks(-1)
            : null;
        if (next is not null && next <= filter.From) next = null;
        return new InfraAuditPage(items, next);
    }

    public async Task<IReadOnlyList<InfraAuditEvent>> ExportAsync(InfraAuditFilter filter, CancellationToken cancellationToken)
    {
        var rows = new List<InfraAuditEvent>();
        var current = filter with { Limit = _options.MaxPageSize };
        while (rows.Count < _options.MaxExportRows)
        {
            var page = await GetPageAsync(current, cancellationToken);
            rows.AddRange(page.Items);
            if (page.NextTo is not { } next) break;
            current = current with { To = next };
        }
        return rows.Count > _options.MaxExportRows ? rows.GetRange(0, _options.MaxExportRows) : rows;
    }

    public async Task<InfraAuditSummary> SummarizeAsync(InfraAuditFilter filter, CancellationToken cancellationToken)
    {
        var raw = await reader.ReadAsync(InfraAuditQuery.Build(filter), filter.From, filter.To, SummaryScanLimit, cancellationToken);
        var events = Normalize(raw, filter).ToList();
        static IReadOnlyList<KeyValuePair<string, int>> Top(IEnumerable<string?> values) =>
            values.Where(value => !string.IsNullOrEmpty(value))
                .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(10).ToList();
        return new InfraAuditSummary(
            filter.From, filter.To, events.Count, raw.Count >= SummaryScanLimit,
            events.GroupBy(item => item.Source).ToDictionary(group => group.Key, group => group.Count()),
            events.GroupBy(item => item.Result).ToDictionary(group => group.Key, group => group.Count()),
            Top(events.Select(item => item.Actor)),
            Top(events.Select(item => item.Target)),
            events.GroupBy(item => new DateTimeOffset(item.Timestamp.UtcDateTime.Year, item.Timestamp.UtcDateTime.Month,
                    item.Timestamp.UtcDateTime.Day, item.Timestamp.UtcDateTime.Hour, 0, 0, TimeSpan.Zero))
                .OrderBy(group => group.Key)
                .Select(group => new KeyValuePair<DateTimeOffset, int>(group.Key, group.Count()))
                .ToList());
    }

    /// <summary>
    /// Warpgate sessions that started in [from, to] and have not logged an end. Lines are read
    /// newest first, so a truncated scan loses only the oldest starts.
    /// </summary>
    public async Task<WarpgateOpenSessions> GetOpenWarpgateSessionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var raw = await reader.ReadAsync(WarpgateSessionTracker.BuildQuery(from, to, SummaryScanLimit),
            from, to, SummaryScanLimit, cancellationToken);
        var events = raw
            .Select(entry => InfraAuditNormalizer.Normalize(entry.Timestamp, entry.Line))
            .OfType<InfraAuditEvent>();
        return new WarpgateOpenSessions(from, to, WarpgateSessionTracker.OpenSessions(events), raw.Count >= SummaryScanLimit);
    }

    private static IEnumerable<InfraAuditEvent> Normalize(
        IEnumerable<(DateTimeOffset Timestamp, string Line)> raw, InfraAuditFilter filter)
    {
        foreach (var (timestamp, line) in raw.OrderByDescending(entry => entry.Timestamp))
        {
            var item = InfraAuditNormalizer.Normalize(timestamp, line);
            if (item is null) continue;
            if (filter.Result is not null && item.Result != filter.Result) continue;
            // The line filter already matched the text; also require it in the actor field.
            if (filter.Actor is not null &&
                (item.Actor is null || !item.Actor.Contains(filter.Actor, StringComparison.OrdinalIgnoreCase)))
                continue;
            // "My activity": the actor must be exactly one of the caller's identifiers.
            if (filter.ActorAny is { Count: > 0 } actors &&
                (item.Actor is null || !actors.Contains(item.Actor, StringComparer.OrdinalIgnoreCase)))
                continue;
            yield return item;
        }
    }
}
