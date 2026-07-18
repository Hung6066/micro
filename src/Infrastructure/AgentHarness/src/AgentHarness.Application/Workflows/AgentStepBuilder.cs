namespace His.Hope.AgentHarness.Application.Workflows;

/// <summary>
/// Concrete builder for configuring an agent step within a pipeline phase.
/// Tracks priority, dependencies, target service, and timeout settings.
/// </summary>
public class AgentStepBuilder : IAgentStepBuilder
{
    public Priority Priority { get; private set; } = Priority.Normal;
    public List<string> Dependencies { get; } = new();
    public string? Service { get; private set; }
    public int TimeoutValue { get; private set; } = 15;
    public TimeUnit TimeoutUnit { get; private set; } = TimeUnit.Minutes;

    public IAgentStepBuilder WithPriority(Priority priority)
    {
        Priority = priority;
        return this;
    }

    public IAgentStepBuilder DependsOn(params string[] agents)
    {
        Dependencies.AddRange(agents);
        return this;
    }

    public IAgentStepBuilder WithService(string service)
    {
        Service = service;
        return this;
    }

    public IAgentStepBuilder Timeout(int value, TimeUnit unit)
    {
        TimeoutValue = value;
        TimeoutUnit = unit;
        return this;
    }

    public IAgentStepBuilder DependsOnAll()
    {
        Dependencies.Add("*");
        return this;
    }
}
