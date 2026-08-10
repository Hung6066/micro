namespace His.Hope.AgentHarness.Application.Workflows;

/// <summary>
/// Defines a pipeline workflow that can be configured via the fluent <see cref="IWorkflowBuilder"/>
/// and can determine whether it matches a given <see cref="ChangeScope"/>.
/// </summary>
public interface IWorkflowDefinition
{
    void Configure(IWorkflowBuilder builder);
    bool MatchesScope(ChangeScope scope) => true;
}

/// <summary>
/// Fluent builder for constructing pipeline workflows programmatically.
/// </summary>
public interface IWorkflowBuilder
{
    IWorkflowBuilder Name(string name);
    IWorkflowBuilder Description(string description);
    IWorkflowBuilder Version(string version);
    IWorkflowBuilder TriggerOn(params string[] paths);
    IPlanBuilder Plan();
    IImplementBuilder Implement();
}

/// <summary>
/// Fluent builder for the Plan phase — configures which agent plans the work
/// and the timeout context.
/// </summary>
public interface IPlanBuilder
{
    IPlanBuilder WithAgent(string agent);
    IPlanBuilder Timeout(int value, TimeUnit unit);
    IPlanBuilder WithContext(Action<PlanContext> configure);
}

/// <summary>
/// Fluent builder for the Implement phase — adds agents that will execute
/// the implementation work.
/// </summary>
public interface IImplementBuilder
{
    IImplementBuilder AddAgent(string name, Action<AgentStepBuilder>? configure = null);
}

/// <summary>
/// Configures a single agent step within a phase — priority, dependencies,
/// target service, and timeout.
/// </summary>
public interface IAgentStepBuilder
{
    IAgentStepBuilder WithPriority(Priority priority);
    IAgentStepBuilder DependsOn(params string[] agents);
    IAgentStepBuilder WithService(string service);
    IAgentStepBuilder Timeout(int value, TimeUnit unit);
    IAgentStepBuilder DependsOnAll();
}

/// <summary>
/// Time unit for timeout configuration.
/// </summary>
public enum TimeUnit
{
    Seconds,
    Minutes
}

/// <summary>
/// Execution priority for an agent step.
/// </summary>
public enum Priority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Provides contextual information for the Plan phase — such as which services
/// to include or whether proto changes are involved.
/// </summary>
public class PlanContext
{
    public List<string> Services { get; } = new();
    public void IncludeServices(params string[] services) => Services.AddRange(services);
    public void IncludeProtoChanges(bool include) { }
}
