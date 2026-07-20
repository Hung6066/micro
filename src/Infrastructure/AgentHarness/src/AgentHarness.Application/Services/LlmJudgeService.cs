using His.Hope.AgentHarness.Core.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace His.Hope.AgentHarness.Application.Services;

public class LlmJudgeService
{
    private readonly ILlmJudgeProvider _provider;

    public LlmJudgeService() : this(CreateDefaultProvider()) { }

    public LlmJudgeService(ILlmJudgeProvider provider)
    {
        _provider = provider;
    }

    private static readonly List<(string pattern, int penalty, string reasoning)> Rules =
    [
        ("error CS", 80, "C# compilation error detected"),
        ("error BC", 80, "VB compilation error detected"),
        ("FAILED", 80, "Test failure detected"),
        ("test failed", 80, "Test failure detected"),
        ("contract violation", 80, "Contract violation detected"),
        ("buf breaking", 80, "Breaking contract change detected"),
        ("schema mismatch", 70, "Schema mismatch detected"),
        ("connection refused", 70, "Infrastructure connectivity issue"),
        ("timeout", 60, "Operation timed out"),
        ("hardcoded secret", 80, "Hardcoded secret detected"),
        ("permissionguard", 60, "Permission guard issue"),
        ("deadlock", 80, "Deadlock detected"),
        ("warning", 50, "Warnings present in output"),
        ("exception", 70, "Exception thrown"),
        ("nullreference", 80, "Null reference error"),
        ("argumentnull", 70, "Null argument error"),
        ("access denied", 70, "Access denied"),
        ("unauthorized", 70, "Unauthorized access"),
        ("not found", 50, "Resource not found"),
    ];

    public virtual JudgeScore EvaluateQuality(string output, string agentName)
    {
        var fallback = EvaluateWithRules(output, agentName);
        var providerScore = _provider.Evaluate(output ?? string.Empty, agentName, fallback);
        return providerScore ?? fallback;
    }

    private static ILlmJudgeProvider CreateDefaultProvider()
    {
        var endpoint = Environment.GetEnvironmentVariable("AGENTHARNESS_LLM_JUDGE_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return new HttpLlmJudgeProvider(uri, Environment.GetEnvironmentVariable("AGENTHARNESS_LLM_JUDGE_API_KEY"));
        }

        return new RuleBasedJudgeProvider();
    }

    private static JudgeScore EvaluateWithRules(string output, string agentName)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new JudgeScore
            {
                NumericScore = 90,
                Reasoning = $"Agent '{agentName}' produced clean output",
                Passed = true
            };
        }

        var totalPenalty = 0;
        var reasons = new List<string>();
        var appliedRules = new HashSet<string>();

        foreach (var (pattern, penalty, reasoning) in Rules)
        {
            if (output.Contains(pattern, StringComparison.OrdinalIgnoreCase) && appliedRules.Add(pattern))
            {
                totalPenalty += penalty;
                reasons.Add(reasoning);
            }
        }

        var score = Math.Max(0, Math.Min(100, 100 - totalPenalty));

        return new JudgeScore
        {
            NumericScore = score,
            Reasoning = reasons.Count > 0
                ? string.Join("; ", reasons)
                : $"Agent '{agentName}' produced clean output",
            Passed = score >= 60
        };
    }

    private sealed class RuleBasedJudgeProvider : ILlmJudgeProvider
    {
        public JudgeScore? Evaluate(string output, string agentName, JudgeScore fallback) => fallback;
    }

    private sealed class HttpLlmJudgeProvider : ILlmJudgeProvider
    {
        private readonly Uri _endpoint;
        private readonly string? _apiKey;

        public HttpLlmJudgeProvider(Uri endpoint, string? apiKey)
        {
            _endpoint = endpoint;
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }

        public JudgeScore? Evaluate(string output, string agentName, JudgeScore fallback)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                if (_apiKey != null)
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var payload = new
                {
                    agent = agentName,
                    output,
                    fallback_score = fallback.NumericScore,
                    rubric = "Score 0-100. Penalize build/test/security/contract failures. Return JSON: { score, reasoning, passed }."
                };

                using var response = client.PostAsJsonAsync(_endpoint, payload).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                using var stream = response.Content.ReadAsStream();
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                var score = root.TryGetProperty("score", out var scoreEl) && scoreEl.ValueKind == JsonValueKind.Number
                    ? scoreEl.GetInt32()
                    : root.TryGetProperty("numeric_score", out var numericEl) && numericEl.ValueKind == JsonValueKind.Number
                        ? numericEl.GetInt32()
                        : fallback.NumericScore;

                var reasoning = root.TryGetProperty("reasoning", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
                    ? reasonEl.GetString()
                    : fallback.Reasoning;

                var passed = root.TryGetProperty("passed", out var passedEl) && passedEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? passedEl.GetBoolean()
                    : score >= 60;

                return new JudgeScore
                {
                    NumericScore = Math.Clamp(score, 0, 100),
                    Reasoning = reasoning,
                    Passed = passed
                };
            }
            catch
            {
                return fallback;
            }
        }
    }
}

public interface ILlmJudgeProvider
{
    JudgeScore? Evaluate(string output, string agentName, JudgeScore fallback);
}
