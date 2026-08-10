using System.Text.Json;
using Serilog;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

/// <summary>
/// Stores an artifact (test results, logs, build output) associated with a pipeline/agent run.
/// Content can be inline (base64) or referenced via storage_path.
/// </summary>
public class SaveArtifactTool
{
    private readonly IStateStore _store;

    public SaveArtifactTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");
        var name = parameters.GetValueOrDefault("name")?.ToString()
            ?? throw new ArgumentException("'name' is required.");

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        var contentType = parameters.GetValueOrDefault("content_type")?.ToString() ?? "text/plain";
        var storagePath = parameters.GetValueOrDefault("storage_path")?.ToString() ?? $"artifacts/{pipelineRunId:N}/{name}";

        byte[]? content = null;
        if (parameters.TryGetValue("content_base64", out var contentObj) && contentObj is JsonElement contentEl)
        {
            var base64 = contentEl.GetString();
            if (!string.IsNullOrEmpty(base64))
                content = Convert.FromBase64String(base64);
        }

        var artifact = Artifact.Create(pipelineRunId, name, contentType, storagePath,
            content?.Length ?? 0, content);
        await _store.SaveArtifactAsync(artifact);

        Log.Information("Artifact saved: {Name} ({Size} bytes) for pipeline {PipelineId}",
            name, artifact.SizeBytes, pipelineRunId);

        return JsonSerializer.Serialize(new
        {
            artifact_id = artifact.Id.ToString(),
            name = artifact.Name,
            size_bytes = artifact.SizeBytes,
            content_type = artifact.ContentType
        });
    }
}

/// <summary>
/// Retrieves an artifact by ID, including its content.
/// </summary>
public class GetArtifactTool
{
    private readonly IStateStore _store;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public GetArtifactTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var artifactIdStr = parameters.GetValueOrDefault("artifact_id")?.ToString()
            ?? throw new ArgumentException("'artifact_id' is required.");

        if (!Guid.TryParse(artifactIdStr, out var artifactId))
            throw new ArgumentException("'artifact_id' must be a valid GUID.");

        var artifact = await _store.GetArtifactAsync(artifactId);
        if (artifact == null)
            throw new InvalidOperationException($"Artifact {artifactId} not found.");

        return JsonSerializer.Serialize(new
        {
            artifact_id = artifact.Id.ToString(),
            pipeline_run_id = artifact.PipelineRunId.ToString(),
            name = artifact.Name,
            content_type = artifact.ContentType,
            size_bytes = artifact.SizeBytes,
            storage_path = artifact.StoragePath,
            content_base64 = artifact.Content != null ? Convert.ToBase64String(artifact.Content) : null,
            created_at = artifact.CreatedAt
        }, JsonOpts);
    }
}
