namespace His.Hope.AgentHarness.Application.Services;

public class CostTracker
{
    private readonly Dictionary<string, AgentCost> _usage = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private static readonly Dictionary<string, decimal> DefaultMaxDailyCost = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = 10.00m,
        ["angular"] = 10.00m,
        ["dba"] = 10.00m,
        ["devops"] = 10.00m,
        ["docs"] = 10.00m,
        ["ml-ai"] = 10.00m,
        ["data-platform"] = 10.00m,
        ["testing-backend"] = 10.00m,
        ["testing-frontend"] = 10.00m,
        ["qa"] = 10.00m,
        ["validate"] = 10.00m,
        ["check-ui"] = 10.00m,
        ["e2e-test"] = 10.00m,
        ["explore"] = 10.00m,
        ["git"] = 10.00m,
        ["security"] = 10.00m,
        ["loop-engineer"] = 10.00m,
        ["harness-runner"] = 10.00m,
    };

    private static readonly decimal EstimatedCostPerCall = 0.05m;

    public void TrackCall(string agentName)
    {
        lock (_lock)
        {
            if (!_usage.TryGetValue(agentName, out var cost))
            {
                cost = new AgentCost();
                _usage[agentName] = cost;
            }
            cost.CallCount++;
            cost.LastCalledAt = DateTime.UtcNow;
        }
    }

    public AgentUsage GetUsage(string agentName)
    {
        lock (_lock)
        {
            if (!_usage.TryGetValue(agentName, out var cost))
                return new AgentUsage(0, 0, 0);

            return new AgentUsage(cost.CallCount, cost.CallCount * EstimatedCostPerCall, DefaultMaxDailyCost.GetValueOrDefault(agentName, 10.00m));
        }
    }

    public bool IsOverBudget(string agentName, decimal? maxDailyCost = null)
    {
        var maxCost = maxDailyCost ?? DefaultMaxDailyCost.GetValueOrDefault(agentName, 10.00m);
        var usage = GetUsage(agentName);
        return usage.EstimatedCost >= maxCost;
    }

    public IReadOnlyDictionary<string, AgentCost> GetAllUsage()
    {
        lock (_lock)
        {
            return _usage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    public void ResetDaily()
    {
        lock (_lock)
        {
            _usage.Clear();
        }
    }
}

public class AgentCost
{
    public int CallCount { get; set; }
    public DateTime? LastCalledAt { get; set; }
}

public readonly record struct AgentUsage(int CallCount, decimal EstimatedCost, decimal MaxDailyCost);
