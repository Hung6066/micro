using Serilog;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Optimizes instinct (MemoryEntry) quality by boosting recently successful
/// entries, decaying stale ones, and merging duplicate patterns.
/// </summary>
public class InstinctOptimizer
{
    private readonly IStateStore _store;

    private static readonly TimeSpan BoostWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(7);
    private const decimal BoostAmount = 0.05m;
    private const decimal DecayFactor = 0.95m;

    public InstinctOptimizer(IStateStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Runs the optimization pass over all memory entries.
    /// </summary>
    public async Task<InstinctOptimizationResultDto> OptimizeAsync(CancellationToken ct = default)
    {
        var entries = await _store.GetMemoryEntriesAsync(ct);
        if (entries.Count == 0)
        {
            return new InstinctOptimizationResultDto
            {
                UpdatedAt = DateTime.UtcNow
            };
        }

        int boosted = 0, decayed = 0;

        // Phase 1: Boost recently used successfully, decay stale entries
        foreach (var entry in entries)
        {
            var age = DateTime.UtcNow - entry.LastUsedAt;

            if (age <= BoostWindow && entry.Success && entry.ConfidenceScore < 1.0m)
            {
                entry.BoostConfidence(BoostAmount);
                boosted++;
            }
            else if (age > StaleThreshold && entry.ConfidenceScore > 0.0m)
            {
                entry.DecayConfidence(DecayFactor);
                decayed++;
            }
        }

        // Phase 2: Merge duplicates (same AgentName + ErrorPattern, case-insensitive)
        var merged = 0;
        var seen = new Dictionary<string, MemoryEntry>(StringComparer.OrdinalIgnoreCase);
        var toRemove = new List<MemoryEntry>();

        foreach (var entry in entries)
        {
            var key = $"{entry.AgentName}|{entry.ErrorPattern}";
            if (seen.TryGetValue(key, out var survivor))
            {
                survivor.MergeFrom(entry);
                toRemove.Add(entry);
                merged++;
            }
            else
            {
                seen[key] = entry;
            }
        }

        // Save all updated entries and delete merged duplicates
        var (saved, removed) = await SaveEntriesAsync(entries, toRemove, ct);

        return new InstinctOptimizationResultDto
        {
            BoostedCount = boosted,
            DecayedCount = decayed,
            MergedCount = merged,
            RecordedCount = saved,
            RemovedCount = removed,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task<(int saved, int removed)> SaveEntriesAsync(List<MemoryEntry> allEntries, List<MemoryEntry> toRemove, CancellationToken ct)
    {
        int saved = 0, removed = 0;
        var removeIds = toRemove.Select(e => e.Id).ToHashSet();

        foreach (var entry in allEntries)
        {
            if (removeIds.Contains(entry.Id))
            {
                // Delete duplicate entries so only the survivor remains
                await _store.DeleteMemoryEntryAsync(entry.Id, ct);
                removed++;
                continue;
            }

            await _store.SaveMemoryEntryAsync(entry, ct);
            saved++;
        }

        return (saved, removed);
    }
}
