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

        var results = new List<bool>();
        var totalJudgeScore = 0;
        var taskCount = 0;

        foreach (var task in tasks)
        {
            for (var attempt = 0; attempt < k; attempt++)
            {
                // Simulate execution by grading the task input as output
                var simulatedOutput = SimulateAgentOutput(task, targetAgent, targetModel);
                var judgeScore = _judge.EvaluateQuality(simulatedOutput, targetAgent);
                var passed = judgeScore.Passed;

                results.Add(passed);
                if (attempt == 0 && passed) taskCount++;
                totalJudgeScore += judgeScore.NumericScore;
            }
        }

        var totalAttempts = tasks.Count * k;
        var passAt1 = totalAttempts > 0
            ? (double)results.Where((_, i) => i % k == 0).Count(r => r) / tasks.Count
            : 0;
        var passAtK = totalAttempts > 0
            ? ComputePassAtK(results, tasks.Count, k)
            : 0;

        var avgJudgeScore = results.Count > 0 ? totalJudgeScore / results.Count : 0;

        var run = EvalRun.Create(suite.Id, targetAgent, targetModel);
        run.Complete(passAt1, passAtK, avgJudgeScore, JsonSerializer.Serialize(results));
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

        // Sort descending by PassAt1
        results = results.OrderByDescending(r => r.PassAt1 ?? 0).ToList();

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

    private static string SimulateAgentOutput(
        EvalTaskDef task,
        string agentName,
        string? modelName)
    {
        // Simple deterministic simulation: match if input contains expected or vice versa
        if (task.Expected != null)
        {
            if (task.Input.Contains(task.Expected, StringComparison.OrdinalIgnoreCase) ||
                task.Expected.Contains(task.Input, StringComparison.OrdinalIgnoreCase))
            {
                return task.Expected;
            }
            return $"Generated output for {agentName}/{modelName}: {task.Input}";
        }

        return $"Generated output for {agentName}/{modelName}: {task.Input}";
    }

    private static double ComputePassAtK(List<bool> results, int numTasks, int k)
    {
        // pass@k: for each task, if any of the k attempts passed, it counts as solved
        var solved = 0;
        for (var t = 0; t < numTasks; t++)
        {
            var taskPassed = false;
            for (var a = 0; a < k; a++)
            {
                var idx = t * k + a;
                if (idx < results.Count && results[idx])
                {
                    taskPassed = true;
                    break;
                }
            }
            if (taskPassed) solved++;
        }
        return numTasks > 0 ? (double)solved / numTasks : 0;
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
            JudgeScore = run.JudgeScoreValue,
            Status = run.Status.ToString(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt
        };
    }
}
