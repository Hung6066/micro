using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Guardrail service — validates actions against policies before execution.
/// Supports whitelist, blocklist, and HITL (human-in-the-loop) approval.
/// </summary>
public class GuardrailService
{
    private static readonly HashSet<string> KnownAgents = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet", "angular", "dba", "devops", "docs", "ml-ai", "data-platform",
        "testing-backend", "testing-frontend", "qa", "validate", "check-ui",
        "e2e-test", "explore", "git", "security", "loop-engineer", "harness-runner"
    };

    private static readonly HashSet<string> DestructiveActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete", "deactivate", "remove", "purge", "revoke", "cancel"
    };

    private readonly List<GuardrailPolicy> _policies;
    private readonly CostTracker _costTracker;

    public GuardrailService() : this(new CostTracker()) { }

    public GuardrailService(CostTracker costTracker)
    {
        _costTracker = costTracker;
        _policies = new List<GuardrailPolicy>
        {
            new() { ActionType = "dispatch-agent", AgentPattern = "*", Action = GuardrailAction.Allow },
            new() { ActionType = "complete-task", AgentPattern = "*", Action = GuardrailAction.Allow },
            new() { ActionType = "cancel-pipeline", AgentPattern = "*", Action = GuardrailAction.RequireApproval,
                    Reason = "Cancelling a running pipeline may lose in-progress work" },
            new() { ActionType = "delete", AgentPattern = "*", Action = GuardrailAction.RequireApproval,
                    Reason = "Delete operations require human verification" },
        };
    }

    /// <summary>
    /// Validates an action against guardrail policies.
    /// Returns a result indicating whether the action is allowed, blocked, or needs approval.
    /// </summary>
    public GuardrailResult Validate(string actionType, string? agentName = null, string? details = null)
    {
        // 0. Check budget when dispatching an agent
        if (actionType == "dispatch-agent" && agentName != null)
        {
            if (_costTracker.IsOverBudget(agentName))
            {
                var usage = _costTracker.GetUsage(agentName);
                return GuardrailResult.Block(
                    $"Agent '{agentName}' is over budget. " +
                    $"Calls today: {usage.CallCount}, Estimated cost: ${usage.EstimatedCost:F2}, " +
                    $"Max daily: ${usage.MaxDailyCost:F2}");
            }
        }

        // 1. Check agent whitelist (for dispatch actions)
        if (actionType == "dispatch-agent" && agentName != null && !KnownAgents.Contains(agentName))
        {
            return GuardrailResult.Block($"Unknown agent '{agentName}'. Allowed: {string.Join(", ", KnownAgents)}");
        }

        // 2. Check destructive action patterns
        foreach (var destructive in DestructiveActions)
        {
            if (actionType.IndexOf(destructive, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GuardrailResult.RequestApproval(
                    $"Destructive action '{actionType}' requires human approval. {details ?? ""}");
            }
        }

        // 3. Check configured policies
        foreach (var policy in _policies)
        {
            if (!MatchesPolicy(policy, actionType, agentName)) continue;

            switch (policy.Action)
            {
                case GuardrailAction.Block:
                    return GuardrailResult.Block(policy.Reason ?? "Blocked by policy");
                case GuardrailAction.RequireApproval:
                    return GuardrailResult.RequestApproval(policy.Reason ?? "Requires approval");
                case GuardrailAction.Allow:
                    return GuardrailResult.Allow();
            }
        }

        // 4. Check unknown action types
        if (actionType.StartsWith("delete", StringComparison.OrdinalIgnoreCase) ||
            actionType.StartsWith("deactivate", StringComparison.OrdinalIgnoreCase) ||
            actionType.StartsWith("revoke", StringComparison.OrdinalIgnoreCase))
        {
            return GuardrailResult.RequestApproval($"Action '{actionType}' requires human approval");
        }

        return GuardrailResult.Allow();
    }

    public bool CanExecuteCode(string agentName)
    {
        return agentName is "dotnet" or "angular" or "devops";
    }

    private static bool MatchesPolicy(GuardrailPolicy policy, string actionType, string? agentName)
    {
        if (!actionType.Equals(policy.ActionType, StringComparison.OrdinalIgnoreCase)) return false;
        if (policy.AgentPattern == "*") return true;
        if (agentName == null) return false;
        return agentName.Equals(policy.AgentPattern, StringComparison.OrdinalIgnoreCase);
    }
}

public class GuardrailResult
{
    public bool IsAllowed { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool NeedsApproval { get; private set; }
    public string? Reason { get; private set; }

    private GuardrailResult() { }

    public static GuardrailResult Allow() => new() { IsAllowed = true };
    public static GuardrailResult Block(string reason) => new() { IsBlocked = true, Reason = reason };
    public static GuardrailResult RequestApproval(string reason) => new() { NeedsApproval = true, Reason = reason };
}
