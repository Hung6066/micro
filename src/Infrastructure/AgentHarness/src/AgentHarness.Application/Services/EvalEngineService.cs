using System.Text.Json;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

public class EvalEngineService
{
    private readonly IStateStore _store;
    private readonly LlmJudgeService _judge;

    public EvalEngineService(IStateStore store, LlmJudgeService judge)
    {
        _store = store;
        _judge = judge;
    }

    public async Task<EvalRunDto> RunSuiteAsync(
        string suiteName,
        string targetAgent,
        string? targetModel,
        int k,
        CancellationToken ct = default)
    {
        if (k <= 0)
            throw new ArgumentException("k must be greater than 0.", nameof(k));

        var suite = await _store.GetEvalSuiteAsync(suiteName, ct)
            ?? throw new ArgumentException($"Eval suite '{suiteName}' not found.", nameof(suiteName));

        var tasks = ExtractTasks(suite.DefinitionJson);
        if (tasks.Count == 0)
        {
            var emptyRun = EvalRun.Create(suite.Id, targetAgent, targetModel);
            emptyRun.Complete(passAt1: 0, passAtK: 0, judgeScore: null, rawResultJson: "[]");
            await _store.SaveEvalRunAsync(emptyRun, ct);
            return MapToDto(emptyRun, suite.Name);
        }

        // Per-task results: each entry is a list of bools, one per attempt
        var perTaskResults = new List<List<(bool Passed, int Score)>>();
        var totalJudgeScore = 0;
        var totalEvaluations = 0;

        foreach (var task in tasks)
        {
            var attemptResults = new List<(bool Passed, int Score)>();
            for (var attempt = 0; attempt < k; attempt++)
            {
                // Deterministic simulation based on suite definition drives pass/fail
                var simulatedOutput = GenerateAgentOutput(task, targetAgent, targetModel, attempt);

                bool passed;
                int score;

                if (task.Expected != null)
                {
                    // Suite definition drives pass/fail: exact match against expected
                    passed = string.Equals(simulatedOutput, task.Expected, StringComparison.Ordinal);
                    score = passed ? 100 : 0;
                }
                else
                {
                    // Open-ended task: use judge for quality evaluation
                    var judgeScore = _judge.EvaluateQuality(simulatedOutput, targetAgent);
                    passed = judgeScore.Passed;
                    score = judgeScore.NumericScore;
                }

                attemptResults.Add((passed, score));
                totalJudgeScore += score;
                totalEvaluations++;
            }
            perTaskResults.Add(attemptResults);
        }

        // pass@1: fraction of first-attempt successes across all tasks
        var firstAttemptPasses = perTaskResults.Count(r => r[0].Passed);
        var passAt1 = (double)firstAttemptPasses / tasks.Count;

        // pass@k: fraction of tasks where ANY of the k attempts succeeded
        var anyAttemptPasses = perTaskResults.Count(taskAttempts => taskAttempts.Any(a => a.Passed));
        var passAtK = (double)anyAttemptPasses / tasks.Count;

        var avgJudgeScore = totalEvaluations > 0 ? totalJudgeScore / totalEvaluations : 0;

        var run = EvalRun.Create(suite.Id, targetAgent, targetModel);
        run.Complete(passAt1, passAtK, avgJudgeScore, JsonSerializer.Serialize(perTaskResults));
        await _store.SaveEvalRunAsync(run, ct);

        return MapToDto(run, suite.Name);
    }

    public async Task<ModelComparisonDto> CompareModelsAsync(
        string suiteName,
        string targetAgent,
        IReadOnlyList<string> modelNames,
        int k,
        CancellationToken ct = default)
    {
        var results = new List<EvalRunDto>();

        foreach (var model in modelNames)
        {
            var run = await RunSuiteAsync(suiteName, targetAgent, model, k, ct);
            results.Add(run);
        }

        // Sort descending by PassAt1, then PassAtK, then JudgeScore
        results = results
            .OrderByDescending(r => r.PassAt1 ?? 0)
            .ThenByDescending(r => r.PassAtK ?? 0)
            .ThenByDescending(r => r.JudgeScore ?? 0)
            .ToList();

        var winnerModel = results.Count > 0
            ? (results[0].TargetModel ?? results[0].TargetAgent)
            : string.Empty;

        return new ModelComparisonDto
        {
            EvalSuiteName = suiteName,
            TargetAgent = targetAgent,
            Results = results,
            WinnerModel = winnerModel
        };
    }

    // ---- Private helpers ----

    private sealed record EvalTaskDef(string Input, string? Expected);

    private static List<EvalTaskDef> ExtractTasks(string definitionJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("tasks", out var tasksEl) && tasksEl.ValueKind == JsonValueKind.Array)
            {
                var tasks = new List<EvalTaskDef>();
                foreach (var item in tasksEl.EnumerateArray())
                {
                    var input = item.TryGetProperty("input", out var inputEl) && inputEl.ValueKind == JsonValueKind.String
                        ? inputEl.GetString() ?? "unknown"
                        : "unknown";
                    var expected = item.TryGetProperty("expected", out var expectedEl) && expectedEl.ValueKind == JsonValueKind.String
                        ? expectedEl.GetString()
                        : null;
                    tasks.Add(new EvalTaskDef(input, expected));
                }
                return tasks;
            }
            return new List<EvalTaskDef>();
        }
        catch (JsonException)
        {
            return new List<EvalTaskDef>();
        }
    }

    /// <summary>
    /// Generates deterministic agent output per attempt using a hash of
    /// (agentName, modelName, taskInput, attemptIndex). The suite definition's
    /// Expected value drives pass/fail semantics: when Expected is present,
    /// the output either matches exactly (pass) or is a clearly wrong answer (fail).
    /// </summary>
    private static string GenerateAgentOutput(
        EvalTaskDef task,
        string agentName,
        string? modelName,
        int attemptIndex)
    {
        if (task.Expected == null)
        {
            return $"Generated output for {agentName}/{modelName}: {task.Input}";
        }

        // Deterministic hash: same inputs always produce the same pass/fail decision
        // This makes results reproducible from the stored suite definition
        if (IsPassingAttempt(agentName, modelName, task.Input, attemptIndex))
        {
            return task.Expected;
        }

        // Produce a clearly wrong answer for failing attempts
        return $"wrong answer for: {task.Input}";
    }

    /// <summary>
    /// Deterministic check using a stable hash of (agent, model, task, attempt).
    /// Different agents/models get different pass rates, making comparisons meaningful.
    /// </summary>
    public static bool IsPassingAttempt(string agentName, string? modelName, string taskInput, int attemptIndex)
    {
        var combined = $"{agentName}|{modelName ?? ""}|{taskInput}|{attemptIndex}";
        var hash = Math.Abs(GetStableHashCode(combined));
        // 60% base pass rate per attempt; different agents/models get different distributions
        return hash % 10 < 6;
    }

    /// <summary>
    /// Stable hash that produces identical results across all .NET versions.
    /// </summary>
    public static int GetStableHashCode(string input)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static EvalRunDto MapToDto(EvalRun run, string suiteName)
    {
        return new EvalRunDto
        {
            EvalRunId = run.Id,
            EvalSuiteName = suiteName,
            TargetAgent = run.TargetAgent,
            TargetModel = run.TargetModel,
            PassAt1 = run.PassAt1,
            PassAtK = run.PassAtK,
            JudgeScore = run.JudgeScore,
            Status = run.Status.ToString(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt
        };
    }
}
