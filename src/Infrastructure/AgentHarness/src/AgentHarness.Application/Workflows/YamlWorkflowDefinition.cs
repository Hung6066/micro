namespace His.Hope.AgentHarness.Application.Workflows;

/// <summary>
/// Describes a pipeline workflow defined in YAML.
/// Supports trigger paths, phase execution with parallel agents,
/// loop-back configuration, and commit settings.
/// </summary>
public class YamlWorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public List<string> TriggerPaths { get; set; } = new();
    public List<string> ExcludePaths { get; set; } = new();
    public List<WorkflowPhase> Phases { get; set; } = new();
    public WorkflowLoopConfig? Loop { get; set; }
    public WorkflowCommitConfig? Commit { get; set; }
}

/// <summary>
/// Represents a single pipeline phase with an optional sequential agent
/// or a set of parallel agent steps.
/// </summary>
public class WorkflowPhase
{
    public string Phase { get; set; } = string.Empty;
    public string? Agent { get; set; }
    public List<WorkflowAgentStep>? Parallel { get; set; }
}

/// <summary>
/// Defines a single agent step within a phase, including dependencies,
/// timeout, condition, and gates.
/// </summary>
public class WorkflowAgentStep
{
    public string Agent { get; set; } = string.Empty;
    public string? Task { get; set; }
    public List<string>? DependsOn { get; set; }
    public int TimeoutMinutes { get; set; } = 15;
    public string? Condition { get; set; }
    public List<string>? Gates { get; set; }
}

/// <summary>
/// Configures loop-back behavior for the pipeline — which agent handles
/// fixes, the maximum number of iterations, the strategy, and the failure trigger.
/// </summary>
public class WorkflowLoopConfig
{
    public string Agent { get; set; } = "loop-engineer";
    public int MaxIterations { get; set; } = 3;
    public string Strategy { get; set; } = "autofix_then_escalate";
    public string OnFailure { get; set; } = "any_gate";
}

/// <summary>
/// Configures commit behaviour — the commit mode, required confidence threshold,
/// and the git agent that performs the commit.
/// </summary>
public class WorkflowCommitConfig
{
    public string Mode { get; set; } = "auto_pr";
    public decimal RequireConfidence { get; set; } = 0.7m;
    public string Agent { get; set; } = "git";
    public List<string>? DependsOn { get; set; }
}
