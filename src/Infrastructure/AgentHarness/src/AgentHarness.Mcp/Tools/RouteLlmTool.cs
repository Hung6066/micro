using System.Text.Json;
using System.Text.Json.Nodes;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class RouteLlmTool
{
    private readonly CostTracker _costTracker;
    private readonly PiiRedactionService _piiRedaction;
    private readonly GuardrailService _guardrails;

    public RouteLlmTool(CostTracker costTracker, PiiRedactionService piiRedaction, GuardrailService guardrails)
    {
        _costTracker = costTracker;
        _piiRedaction = piiRedaction;
        _guardrails = guardrails;
    }

    private static readonly Dictionary<string, ModelInfo> KnownModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deepseek-v4-flash"] = new("deepseek-v4-flash", 1, 0.01m, "Cheapest, fastest. Good for code gen, simple queries, linting."),
        ["gpt-5.4-mini"]     = new("gpt-5.4-mini", 2, 0.02m, "Fast mid-tier. Good for unit tests, refactoring, docs."),
        ["deepseek-v4-pro"]  = new("deepseek-v4-pro", 3, 0.05m, "Strong reasoning. Good for architecture, security review, complex bugs."),
        ["gpt-5.5"]          = new("gpt-5.5", 4, 0.10m, "Most capable. Good for HIPAA compliance audit, production-critical decisions."),
    };

    private static readonly Dictionary<string, int> CategoryComplexity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["simple"]             = 1,
        ["moderate"]           = 2,
        ["moderate+"]          = 3,
        ["complex"]            = 4,
        ["security_sensitive"] = 4,
    };

    private static double ExtractDouble(Dictionary<string, object> dict, string key, double defaultValue)
    {
        if (dict.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                return je.GetDouble();
        }
        return defaultValue;
    }

    public Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var taskDescription = parameters.GetValueOrDefault("task_description")?.ToString()
            ?? throw new ArgumentException("'task_description' is required.");
        var taskCategory = parameters.GetValueOrDefault("task_category")?.ToString() ?? "moderate";
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString() ?? "unknown";
        var redactPii = parameters.TryGetValue("redact_pii", out var rp) && (
            rp is bool b ? b :
            rp is JsonElement je && je.ValueKind == JsonValueKind.True);
        var availableModelsRaw = parameters.GetValueOrDefault("available_models");
        JsonArray? explicitModels = null;
        if (availableModelsRaw is JsonElement arr && arr.ValueKind == JsonValueKind.Array)
        {
            explicitModels = JsonSerializer.Deserialize<JsonArray>(arr.GetRawText());
        }

        // 1. Redact PII — always for security_sensitive, otherwise on request
        var isSecuritySensitive = string.Equals(taskCategory, "security_sensitive", StringComparison.OrdinalIgnoreCase);
        bool shouldRedact = redactPii || isSecuritySensitive;
        string processedTask = taskDescription;
        bool piiRedacted = false;
        if (shouldRedact)
        {
            var redacted = _piiRedaction.Redact(taskDescription);
            piiRedacted = redacted != taskDescription;
            processedTask = redacted;
        }

        // 2. Determine required capability level
        var requiredLevel = CategoryComplexity.GetValueOrDefault(taskCategory, 2);

        // 3. Filter available models by capability
        var modelsToConsider = KnownModels;
        if (explicitModels != null)
        {
            var explicitNames = explicitModels
                .Select(e => e?.ToString())
                .Where(x => x != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            modelsToConsider = KnownModels.Where(m => explicitNames.Contains(m.Key))
                .ToDictionary(m => m.Key, m => m.Value, StringComparer.OrdinalIgnoreCase);
        }

        // 4. Pick cheapest model that meets capability requirement
        var candidates = modelsToConsider
            .Where(m => m.Value.CapabilityLevel >= requiredLevel)
            .OrderBy(m => m.Value.CostPerCall)
            .ToList();

        var bestModel = candidates.FirstOrDefault();
        if (bestModel.Value == null)
        {
            // Fallback: pick highest capability available
            bestModel = modelsToConsider.MaxBy(m => m.Value.CapabilityLevel);
        }

        // 5. Check budget and guardrails
        var usage = _costTracker.GetUsage(agentName);
        var overBudget = _costTracker.IsOverBudget(agentName);
        var guardrailResult = _guardrails.Validate("route-llm", agentName);
        var isBlocked = guardrailResult.IsBlocked;

        if (bestModel.Value == null)
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                status = "no_model_available",
                recommended_model = "",
                estimated_cost = 0,
                budget_remaining = 0,
                over_budget = overBudget,
                pii_redacted = piiRedacted,
                warning = "No models available for the requested task category."
            }));
        }

        // 6. If over budget, downgrade to cheapest model
        var finalModel = overBudget ? KnownModels.MinBy(m => m.Value.CostPerCall) : bestModel;
        var estimatedCost = (usage.CallCount + 1) * finalModel.Value.CostPerCall;

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            status = isBlocked ? "blocked" : "ok",
            recommended_model = finalModel.Key,
            model_description = finalModel.Value.Description,
            task_category = taskCategory,
            required_capability_level = requiredLevel,
            estimated_cost = estimatedCost,
            budget_remaining = usage.MaxDailyCost - estimatedCost,
            over_budget = overBudget,
            blocked = isBlocked,
            pii_redacted = piiRedacted,
            redacted_task = piiRedacted ? processedTask : null,
            agent_name = agentName
        }));
    }

    private sealed record ModelInfo(string Name, int CapabilityLevel, decimal CostPerCall, string Description);
}
