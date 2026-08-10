using Pgvector;

namespace His.Hope.AgentHarness.Core.Models;

/// <summary>
/// Stores a resolved error pattern + fix for the Loop Engineer.
/// The engine queries past memories to find known fixes before
/// attempting new error classification.
///
/// Similarity is computed by normalized keyword matching
/// (no external embedding API required).
/// </summary>
public class MemoryEntry
{
    public Guid Id { get; private set; }
    public string ErrorPattern { get; private set; } = string.Empty;
    public string ErrorCategory { get; private set; } = string.Empty;
    public string AgentName { get; private set; } = string.Empty;
    public string FixDescription { get; private set; } = string.Empty;
    public string? FixArtifactRef { get; private set; }
    public bool Success { get; private set; }
    public int UseCount { get; private set; }
    public decimal ConfidenceScore { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }

    /// <summary>Normalized keywords for matching (space-separated, lowercase).</summary>
    public string Keywords { get; private set; } = string.Empty;

    /// <summary>256-dim embedding vector for semantic similarity search.</summary>
    public Vector? Embedding { get; set; }

    private MemoryEntry() { }

    public static MemoryEntry Create(
        string errorPattern,
        string errorCategory,
        string agentName,
        string fixDescription,
        string? fixArtifactRef = null,
        bool success = true,
        float[]? embedding = null)
    {
        return new MemoryEntry
        {
            Id = Guid.NewGuid(),
            ErrorPattern = errorPattern,
            ErrorCategory = errorCategory,
            AgentName = agentName,
            FixDescription = fixDescription,
            FixArtifactRef = fixArtifactRef,
            Success = success,
            UseCount = 1,
            ConfidenceScore = 0.85m,
            Keywords = ExtractKeywords(errorPattern + " " + errorCategory + " " + agentName),
            Embedding = embedding is null ? null : new Vector(embedding),
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };
    }

    public void RecordHit()
    {
        UseCount++;
        LastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increases confidence by the given amount, capped at 1.0.
    /// </summary>
    public void BoostConfidence(decimal amount)
    {
        ConfidenceScore = Math.Min(1.0m, ConfidenceScore + amount);
    }

    /// <summary>
    /// Decays confidence by multiplying with the given factor, floored at 0.0.
    /// </summary>
    public void DecayConfidence(decimal factor)
    {
        ConfidenceScore = Math.Max(0.0m, ConfidenceScore * factor);
    }

    /// <summary>
    /// Merges another MemoryEntry into this one.
    /// Combines UseCount, keeps the higher confidence score,
    /// and retains the more recent LastUsedAt.
    /// The other entry's FixDescription and FixArtifactRef are preserved
    /// if they are non-empty and this entry's are empty.
    /// </summary>
    public void MergeFrom(MemoryEntry other)
    {
        UseCount += other.UseCount;
        ConfidenceScore = Math.Max(ConfidenceScore, other.ConfidenceScore);
        if (other.LastUsedAt > LastUsedAt)
            LastUsedAt = other.LastUsedAt;
        if (string.IsNullOrWhiteSpace(FixDescription) && !string.IsNullOrWhiteSpace(other.FixDescription))
            FixDescription = other.FixDescription;
        if (string.IsNullOrWhiteSpace(FixArtifactRef) && !string.IsNullOrWhiteSpace(other.FixArtifactRef))
            FixArtifactRef = other.FixArtifactRef;
    }

    /// <summary>
    /// Extracts normalized keywords from raw text.
    /// Lowercases, removes punctuation, splits by whitespace.
    /// </summary>
    public static string ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var cleaned = new string(text
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : ' ')
            .ToArray());

        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)          // Skip short words
            .Distinct();

        return string.Join(" ", words);
    }

    /// <summary>
    /// Computes Jaccard similarity between two keyword sets.
    /// Returns 0.0 (no match) to 1.0 (identical).
    /// </summary>
    public static double ComputeSimilarity(string keywords1, string keywords2)
    {
        if (string.IsNullOrWhiteSpace(keywords1) || string.IsNullOrWhiteSpace(keywords2))
            return 0.0;

        var set1 = keywords1.Split(' ').ToHashSet();
        var set2 = keywords2.Split(' ').ToHashSet();

        if (set1.Count == 0 || set2.Count == 0) return 0.0;

        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}
