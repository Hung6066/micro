using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Application.Services;

public class LlmJudgeService
{
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

    public JudgeScore EvaluateQuality(string output, string agentName)
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
}
