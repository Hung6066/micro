using System.Reflection;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Plugins;

public class PluginLoader
{
    private readonly List<IAgentPlugin> _plugins = new();

    public IReadOnlyList<IAgentPlugin> Plugins => _plugins.AsReadOnly();

    public void LoadFromAssembly(Assembly assembly)
    {
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IAgentPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false, IsClass: true });

        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IAgentPlugin plugin)
            {
                _plugins.Add(plugin);
            }
        }
    }

    public void LoadFromPath(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        LoadFromAssembly(assembly);
    }

    public async Task InvokeOnAgentStartedAsync(AgentRun agentRun, CancellationToken ct = default)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnAgentStartedAsync(agentRun, ct);
        }
    }

    public async Task InvokeOnAgentCompletedAsync(AgentRun agentRun, CancellationToken ct = default)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnAgentCompletedAsync(agentRun, ct);
        }
    }

    public async Task InvokeOnAgentFailedAsync(AgentRun agentRun, CancellationToken ct = default)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnAgentFailedAsync(agentRun, ct);
        }
    }

    public async Task InvokeOnPipelineCompletedAsync(PipelineRun pipelineRun, CancellationToken ct = default)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnPipelineCompletedAsync(pipelineRun, ct);
        }
    }
}
