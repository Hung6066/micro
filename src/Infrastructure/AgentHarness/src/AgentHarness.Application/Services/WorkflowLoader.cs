using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Loads workflow definitions from YAML files in the workflows/ directory.
/// Each workflow defines pipeline structure: agents, phases, quality gates, loop config.
/// </summary>
public class WorkflowLoader
{
    private readonly string _workflowDir;

    public WorkflowLoader(string? workflowDir = null)
    {
        _workflowDir = workflowDir ?? Path.Combine(AppContext.BaseDirectory, "workflows");
    }

    public WorkflowDefinition? Load(string workflowId)
    {
        // Try exact match
        var path = Path.Combine(_workflowDir, $"{workflowId}.yaml");
        if (File.Exists(path))
            return ParseFile(path);

        // Try with common prefixes
        var safeId = workflowId.Replace('/', '_').Replace('\\', '_');
        path = Path.Combine(_workflowDir, $"{safeId}.yaml");
        if (File.Exists(path))
            return ParseFile(path);

        // Try search
        if (Directory.Exists(_workflowDir))
        {
            var files = Directory.GetFiles(_workflowDir, "*.yaml");
            foreach (var file in files)
            {
                var def = ParseFile(file);
                if (def != null && string.Equals(def.Name, workflowId, StringComparison.OrdinalIgnoreCase))
                    return def;
            }
        }

        return null;
    }

    public List<WorkflowDefinition> ListAll()
    {
        var result = new List<WorkflowDefinition>();
        if (!Directory.Exists(_workflowDir)) return result;

        foreach (var file in Directory.GetFiles(_workflowDir, "*.yaml"))
        {
            var def = ParseFile(file);
            if (def != null) result.Add(def);
        }
        return result;
    }

    private WorkflowDefinition? ParseFile(string path)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var def = deserializer.Deserialize<WorkflowDefinition>(yaml);
            def.SourcePath = path;
            return def;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Represents a parsed workflow definition from YAML.
/// Maps to the workflow YAML format used in the project.
/// </summary>
public class WorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public WorkflowTriggers? Triggers { get; set; }
    public WorkflowPipeline? Pipeline { get; set; }
    public WorkflowLoop? Loop { get; set; }
    public string? SourcePath { get; set; }

    /// <summary>
    /// Converts pipeline stages to a flat task list for the DAG.
    /// </summary>
    public List<(string Phase, string Agent, string Task)> ToTaskList()
    {
        var tasks = new List<(string, string, string)>();
        if (Pipeline == null) return tasks;

        // Implement phase
        if (Pipeline.Implement != null)
        {
            foreach (var node in Pipeline.Implement.Parallel ?? [])
                tasks.Add(("Implement", node.Agent, BuildTask(node)));

            foreach (var item in Pipeline.Implement.Sequential ?? [])
                tasks.Add(("Implement", item.Agent, BuildTask(item)));
        }

        // Test phase
        if (Pipeline.Test != null)
        {
            foreach (var item in Pipeline.Test)
                tasks.Add(("Test", item.Agent, BuildTask(item)));
        }

        // Validate phase
        if (Pipeline.Validate != null)
        {
            foreach (var item in Pipeline.Validate)
                tasks.Add(("Validate", item.Agent, BuildTask(item)));
        }

        // Commit phase
        if (Pipeline.Commit is { Agent: not null })
        {
            tasks.Add(("Commit", Pipeline.Commit.Agent, Pipeline.Commit.Task ?? "Commit changes"));
        }

        return tasks;
    }

    private static string BuildTask(dynamic node) => node.Task ?? $"Execute {node.Agent} for {node.Phase}";
}

public class WorkflowTriggers
{
    public List<string>? Paths { get; set; }
}

public class WorkflowPipeline
{
    public WorkflowImplement? Implement { get; set; }
    public List<WorkflowNode>? Test { get; set; }
    public List<WorkflowNode>? Validate { get; set; }
    public WorkflowCommit? Commit { get; set; }
}

public class WorkflowImplement
{
    public List<WorkflowNode>? Parallel { get; set; }
    public List<WorkflowNode>? Sequential { get; set; }
}

public class WorkflowNode
{
    public string Agent { get; set; } = string.Empty;
    public string? Task { get; set; }
    public string? Phase { get; set; }
    public int TimeoutMinutes { get; set; } = 15;
}

public class WorkflowCommit
{
    public string? Agent { get; set; }
    public string? Task { get; set; }
    public string Mode { get; set; } = "auto_pr";
    public double RequireConfidence { get; set; } = 0.7;
}

public class WorkflowLoop
{
    public string Agent { get; set; } = "loop-engineer";
    public int MaxIterations { get; set; } = 3;
    public string Strategy { get; set; } = "autofix_then_escalate";
}
