using System.Text.RegularExpressions;

namespace His.Hope.AgentHarness.Application.Services;

public class EmbeddingService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "shall", "can", "need", "dare", "ought",
        "used", "to", "of", "in", "for", "on", "with", "at", "by", "from",
        "as", "into", "through", "during", "before", "after", "above", "below",
        "between", "out", "off", "over", "under", "again", "further", "then",
        "once", "here", "there", "when", "where", "why", "how", "all", "each",
        "every", "both", "few", "more", "most", "other", "some", "such", "no",
        "nor", "not", "only", "own", "same", "so", "than", "too", "very",
        "just", "because", "but", "and", "or", "if", "while", "although",
        "this", "that", "these", "those", "it", "its", "it's"
    };

    public float[] GenerateEmbedding(string text)
    {
        var embedding = new float[256];
        var words = Tokenize(text);
        if (words.Count == 0) return embedding;

        foreach (var word in words)
        {
            int hash = StableHash(word) & 0xFF;
            embedding[hash] += 1.0f;
        }

        float magnitude = 0;
        for (int i = 0; i < embedding.Length; i++)
            magnitude += embedding[i] * embedding[i];
        magnitude = MathF.Sqrt(magnitude);

        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
                embedding[i] /= magnitude;
        }

        return embedding;
    }

    public double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        double magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude > 0 ? dot / magnitude : 0;
    }

    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9_]+", " ");
        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct()
            .ToList();

        return words;
    }

    private static int StableHash(string word)
    {
        uint hash = 5381;
        foreach (char c in word)
            hash = ((hash << 5) + hash) ^ c;
        return (int)hash;
    }
}
