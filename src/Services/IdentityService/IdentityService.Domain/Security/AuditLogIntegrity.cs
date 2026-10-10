using System.Security.Cryptography;
using System.Text;

namespace His.Hope.IdentityService.Domain.Entities;

public sealed record AuditLogIntegrityVerificationResult(
    bool IsValid,
    int EntriesChecked,
    int? InvalidIndex = null,
    string? FailureReason = null,
    long? ExpectedSequence = null,
    long? ActualSequence = null);

/// <summary>
/// Computes and verifies the tamper-evident link for append-only audit entries.
/// The canonical form is deliberately length-prefixed so optional values cannot
/// create ambiguous concatenations.
/// </summary>
public static class AuditLogIntegrity
{
    public static string ComputeHash(AuditLog entry, string? previousIntegrityHash)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var canonical = string.Join('|',
            Field(entry.Id.ToString("D")),
            Field(entry.UserId),
            Field(entry.UserName),
            Field(entry.Action),
            Field(entry.ResourceType),
            Field(entry.ResourceId),
            Field(entry.Details),
            Field(entry.IpAddress),
            Field(entry.UserAgent),
            Field(entry.CorrelationId),
            Field(entry.Outcome),
            Field(entry.BeforeJson),
            Field(entry.AfterJson),
            Field(entry.Source),
            Field(NormalizeTimestamp(entry.Timestamp).ToString("O")),
            Field(entry.IntegritySequence?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field(previousIntegrityHash));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool VerifyChain(IReadOnlyList<AuditLog> entries)
        => VerifyChainDetailed(entries).IsValid;

    public static AuditLogIntegrityVerificationResult VerifyChainDetailed(
        IReadOnlyList<AuditLog> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string? previous = null;
        long? previousSequence = null;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.IntegritySequence is null)
                return Invalid(entries, index, "missing-sequence", previousSequence, null);

            if (previousSequence is not null && entry.IntegritySequence <= previousSequence)
                return Invalid(entries, index, "non-increasing-sequence", previousSequence + 1, entry.IntegritySequence);

            if (!string.Equals(entry.PreviousIntegrityHash, previous, StringComparison.Ordinal))
                return Invalid(entries, index, "previous-hash-mismatch", null, entry.IntegritySequence);

            if (string.IsNullOrWhiteSpace(entry.IntegrityHash))
                return Invalid(entries, index, "missing-hash", null, entry.IntegritySequence);

            var expectedHash = ComputeHash(entry, previous);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(entry.IntegrityHash),
                    Encoding.ASCII.GetBytes(expectedHash)))
                return Invalid(entries, index, "hash-mismatch", null, entry.IntegritySequence);

            previous = entry.IntegrityHash;
            previousSequence = entry.IntegritySequence;
        }

        return new AuditLogIntegrityVerificationResult(true, entries.Count);
    }

    private static AuditLogIntegrityVerificationResult Invalid(
        IReadOnlyList<AuditLog> entries,
        int index,
        string reason,
        long? expectedSequence,
        long? actualSequence)
        => new(false, index, index, reason, expectedSequence, actualSequence);

    private static string Field(string? value) => value is null
        ? "-1:"
        : $"{value.Length}:{value}";

    // PostgreSQL timestamp/timestamptz stores microsecond precision. Hash the
    // persisted representation so verification survives provider round-trip.
    private static DateTime NormalizeTimestamp(DateTime value)
    {
        var utc = value.ToUniversalTime();
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond,
            DateTimeKind.Utc);
    }
}
