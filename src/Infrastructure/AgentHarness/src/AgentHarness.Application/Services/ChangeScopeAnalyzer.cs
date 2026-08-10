using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

public class ChangeScope
{
    public List<string> TriggeredAgents { get; set; } = new();
    public List<PipelinePhase> PhasesToSkip { get; set; } = new();
    public List<string> ChangedFiles { get; set; } = new();
    public bool HasMigrationChanges { get; set; }
    public bool HasProtoChanges { get; set; }
    public bool HasSecurityChanges { get; set; }
}

public class ChangeScopeAnalyzer
{
    private static readonly Dictionary<string, List<string>> PathAgentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["src/Backend/"] = new() { "dotnet", "dba" },
        ["src/Services/"] = new() { "dotnet", "dba" },
        ["src/Shared/"] = new() { "dotnet" },
        ["src/Frontend/"] = new() { "angular" },
        ["k8s/"] = new() { "devops" },
        ["cicd/"] = new() { "devops" },
        ["protos/"] = new() { "dotnet", "angular" },
        ["src/Shared/Protos/"] = new() { "dotnet", "angular" },
        ["docs/"] = new() { "docs" },
        ["vault/"] = new() { "security" },
    };

    public ChangeScope Analyze(IEnumerable<string> changedFiles)
    {
        var files = changedFiles.ToList();
        var scope = new ChangeScope
        {
            ChangedFiles = files,
            HasMigrationChanges = files.Any(f => f.Contains("Migrations/", StringComparison.OrdinalIgnoreCase)),
            HasProtoChanges = files.Any(f => f.EndsWith(".proto", StringComparison.OrdinalIgnoreCase)),
            HasSecurityChanges = files.Any(f => f.Contains("Auth/", StringComparison.OrdinalIgnoreCase) || f.Contains("vault/", StringComparison.OrdinalIgnoreCase))
        };

        var triggeredAgents = new HashSet<string>();
        foreach (var file in files)
        {
            foreach (var (pathPattern, agents) in PathAgentMap)
            {
                if (file.StartsWith(pathPattern, StringComparison.OrdinalIgnoreCase))
                    foreach (var agent in agents) triggeredAgents.Add(agent);
            }
        }
        scope.TriggeredAgents = triggeredAgents.ToList();

        // Phase skip logic
        if (!triggeredAgents.Contains("dotnet") && !triggeredAgents.Contains("angular"))
        {
            scope.PhasesToSkip.Add(PipelinePhase.Implement);
            scope.PhasesToSkip.Add(PipelinePhase.Test);
        }
        if (triggeredAgents.Count == 1 && triggeredAgents.Contains("docs"))
        {
            scope.PhasesToSkip = new List<PipelinePhase> { PipelinePhase.Implement, PipelinePhase.Test };
        }

        return scope;
    }
}
