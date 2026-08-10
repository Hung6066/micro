namespace His.Hope.AgentHarness.Application.Workflows;

/// <summary>
/// Central registry of pipeline workflow definitions.
/// Supports registration of both code-based <see cref="IWorkflowDefinition"/> and
/// YAML-based <see cref="YamlWorkflowDefinition"/> workflows, and provides
/// resolution by name and automatic selection based on <see cref="ChangeScope"/>.
/// </summary>
public class WorkflowRegistry
{
    private readonly Dictionary<string, IWorkflowDefinition> _workflows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a code-based workflow definition.</summary>
    public void Register(IWorkflowDefinition workflow) =>
        _workflows[workflow.GetType().Name] = workflow;

    /// <summary>
    /// Registers a YAML-based workflow definition by wrapping it in an adapter.
    /// </summary>
    public void RegisterYaml(YamlWorkflowDefinition yamlDef) =>
        _workflows[yamlDef.Name] = new YamlWorkflowAdapter(yamlDef);

    /// <summary>Resolves a workflow by name.</summary>
    public IWorkflowDefinition? Resolve(string name) =>
        _workflows.GetValueOrDefault(name);

    /// <summary>
    /// Automatically selects the best-matching workflow for the given change scope.
    /// Falls back to <c>"default-full-pipeline"</c> when no workflow matches.
    /// </summary>
    public IWorkflowDefinition AutoSelect(ChangeScope scope) =>
        _workflows.Values.FirstOrDefault(w => w.MatchesScope(scope))
        ?? _workflows["default-full-pipeline"];

    /// <summary>
    /// Adapter that wraps a <see cref="YamlWorkflowDefinition"/> as an <see cref="IWorkflowDefinition"/>,
    /// enabling YAML-based workflows to participate in the fluent registration system.
    /// </summary>
    private class YamlWorkflowAdapter : IWorkflowDefinition
    {
        private readonly YamlWorkflowDefinition _def;

        public YamlWorkflowAdapter(YamlWorkflowDefinition def) => _def = def;

        public void Configure(IWorkflowBuilder builder)
        {
            // YAML-based workflows are already configured; no-op for the fluent API.
        }

        public bool MatchesScope(ChangeScope scope) =>
            _def.TriggerPaths.Any(tp =>
                scope.ChangedFiles.Any(f => f.StartsWith(tp, StringComparison.OrdinalIgnoreCase)));
    }
}
