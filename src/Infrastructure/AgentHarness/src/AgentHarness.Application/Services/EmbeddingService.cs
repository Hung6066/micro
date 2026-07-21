using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace His.Hope.AgentHarness.Application.Services;

public class EmbeddingService
{
    private const int HarnessVectorSize = 256;
    private readonly IEmbeddingProvider _provider;

    public EmbeddingService() : this(CreateDefaultProvider()) { }

    public EmbeddingService(IEmbeddingProvider provider)
    {
        _provider = provider;
    }

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
        var embedding = _provider.Generate(text ?? string.Empty);
        return Normalize(ProjectToHarnessVectorSize(embedding));
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

    private static IEmbeddingProvider CreateDefaultProvider()
    {
        var endpoint = Environment.GetEnvironmentVariable("AGENTHARNESS_EMBEDDING_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return new HttpEmbeddingProvider(uri, Environment.GetEnvironmentVariable("AGENTHARNESS_EMBEDDING_API_KEY"));
        }

        return new HashEmbeddingProvider();
    }

    private static float[] ProjectToHarnessVectorSize(float[] embedding)
    {
        if (embedding.Length == HarnessVectorSize) return embedding;

        var projected = new float[HarnessVectorSize];
        if (embedding.Length == 0) return projected;

        for (var i = 0; i < embedding.Length; i++)
        {
            projected[i % HarnessVectorSize] += embedding[i];
        }

        return projected;
    }

    private static float[] Normalize(float[] embedding)
    {
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

    private sealed class HashEmbeddingProvider : IEmbeddingProvider
    {
        public float[] Generate(string text)
        {
            var embedding = new float[HarnessVectorSize];
            var words = Tokenize(text);
            if (words.Count == 0) return embedding;

            foreach (var word in words)
            {
                int hash = StableHash(word) & 0xFF;
                embedding[hash] += 1.0f;
            }

            return embedding;
        }
    }

    private sealed class HttpEmbeddingProvider : IEmbeddingProvider
    {
        private readonly Uri _endpoint;
        private readonly string? _apiKey;

        public HttpEmbeddingProvider(Uri endpoint, string? apiKey)
        {
            _endpoint = endpoint;
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }

        public float[] Generate(string text)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                if (_apiKey != null)
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var response = client.PostAsJsonAsync(_endpoint, new { input = text }).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                using var stream = response.Content.ReadAsStream();
                using var doc = JsonDocument.Parse(stream);
                return ExtractEmbedding(doc.RootElement) ?? new HashEmbeddingProvider().Generate(text);
            }
            catch
            {
                return new HashEmbeddingProvider().Generate(text);
            }
        }

        private static float[]? ExtractEmbedding(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
                return ReadFloatArray(root);

            if (root.TryGetProperty("embedding", out var embedding))
                return ReadFloatArray(embedding);

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.TryGetProperty("embedding", out var openAiEmbedding))
                    return ReadFloatArray(openAiEmbedding);
            }

            return null;
        }

        private static float[]? ReadFloatArray(JsonElement array)
        {
            if (array.ValueKind != JsonValueKind.Array) return null;
            return array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetSingle())
                .ToArray();
        }
    }

    private static List<string> Tokenize(string text)
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

public interface IEmbeddingProvider
{
    float[] Generate(string text);
}
