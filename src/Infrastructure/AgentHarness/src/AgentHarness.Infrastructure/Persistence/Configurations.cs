using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Persistence;

public class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> builder)
    {
        builder.ToTable("pipeline_runs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.WorkflowId).HasColumnName("workflow_id").HasMaxLength(256);
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion<string>();
        builder.Ignore(p => p.DagDefinition);
        builder.Property(p => p.Parameters).HasColumnName("parameters").HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new());
        builder.Property(p => p.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(64);
        builder.Property(p => p.StartedAt).HasColumnName("started_at");
        builder.Property(p => p.CompletedAt).HasColumnName("completed_at");
        builder.Property(p => p.TimeoutAt).HasColumnName("timeout_at");
        builder.Property(p => p.Metadata).HasColumnName("metadata").HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new());
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(p => p.Status).HasDatabaseName("ix_pipeline_runs_status");
    }
}

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(a => a.AgentName).HasColumnName("agent_name").HasMaxLength(128);
        builder.Property(a => a.TaskDescription).HasColumnName("task_description").HasColumnType("text");
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion<string>();
        builder.Property(a => a.AttemptNumber).HasColumnName("attempt_number");
        builder.Property(a => a.ConfidenceScore).HasColumnName("confidence_score").HasPrecision(3, 2);
        builder.Property(a => a.StartedAt).HasColumnName("started_at");
        builder.Property(a => a.CompletedAt).HasColumnName("completed_at");
        builder.Property(a => a.OutputArtifactRef).HasColumnName("output_artifact_ref").HasMaxLength(512);
        builder.Property(a => a.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(a => a.RetryCount).HasColumnName("retry_count");
        builder.Property(a => a.MaxRetries).HasColumnName("max_retries");
        builder.Property(a => a.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(a => a.CircuitState)
            .HasColumnName("circuit_state")
            .HasMaxLength(16)
            .HasConversion<string>();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(a => a.PipelineRunId).HasDatabaseName("ix_agent_runs_pipeline_run_id");
        builder.HasIndex(a => a.AgentName).HasDatabaseName("ix_agent_runs_agent_name");
    }
}

public class QualityGateConfiguration : IEntityTypeConfiguration<QualityGate>
{
    public void Configure(EntityTypeBuilder<QualityGate> builder)
    {
        builder.ToTable("quality_gate_results");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(g => g.GateId).HasColumnName("gate_id").HasMaxLength(128);
        builder.Property(g => g.GateType).HasColumnName("gate_name").HasMaxLength(256);
        builder.Property(g => g.Passed).HasColumnName("passed");
        builder.Property(g => g.Details).HasColumnName("details").HasColumnType("text");
        builder.Property(g => g.EvaluatedAt).HasColumnName("evaluated_at");
        builder.HasIndex(g => g.PipelineRunId).HasDatabaseName("ix_quality_gate_results_pipeline_run_id");
    }
}

public class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("artifacts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(256);
        builder.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(128);
        builder.Property(a => a.StoragePath).HasColumnName("storage_ref").HasMaxLength(512);
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes");
        builder.Property(a => a.Content).HasColumnName("content").HasColumnType("bytea");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(a => a.PipelineRunId).HasDatabaseName("ix_artifacts_pipeline_run_id");
    }
}

public class PipelineCheckpointConfiguration : IEntityTypeConfiguration<PipelineCheckpoint>
{
    public void Configure(EntityTypeBuilder<PipelineCheckpoint> builder)
    {
        builder.ToTable("pipeline_checkpoints");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PipelineRunId).HasColumnName("pipeline_run_id");
        builder.Property(c => c.Phase).HasColumnName("phase").HasMaxLength(32);
        builder.Property(c => c.CompletedNodeIds).HasColumnName("completed_node_ids").HasColumnType("jsonb");
        builder.Property(c => c.FailedNodeIds).HasColumnName("failed_node_ids").HasColumnType("jsonb");
        builder.Property(c => c.NodeStatesJson).HasColumnName("node_states").HasColumnType("jsonb");
        builder.Property(c => c.LoopIteration).HasColumnName("loop_iteration");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(c => c.PipelineRunId).HasDatabaseName("ix_checkpoints_pipeline_run_id");
        builder.HasIndex(c => new { c.PipelineRunId, c.CreatedAt }).HasDatabaseName("ix_checkpoints_pipeline_run_created");
    }
}

public class MemoryEntryConfiguration : IEntityTypeConfiguration<MemoryEntry>
{
    public void Configure(EntityTypeBuilder<MemoryEntry> builder)
    {
        builder.ToTable("memory_entries");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ErrorPattern).HasColumnName("error_pattern").HasColumnType("text");
        builder.Property(m => m.ErrorCategory).HasColumnName("error_category").HasMaxLength(50);
        builder.Property(m => m.AgentName).HasColumnName("agent_name").HasMaxLength(128);
        builder.Property(m => m.FixDescription).HasColumnName("fix_description").HasColumnType("text");
        builder.Property(m => m.FixArtifactRef).HasColumnName("fix_artifact_ref").HasMaxLength(512);
        builder.Property(m => m.Success).HasColumnName("success");
        builder.Property(m => m.UseCount).HasColumnName("use_count");
        builder.Property(m => m.Keywords).HasColumnName("keywords").HasColumnType("text");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.LastUsedAt).HasColumnName("last_used_at");
        builder.HasIndex(m => m.AgentName).HasDatabaseName("ix_memory_agent_name");
        builder.HasIndex(m => m.ErrorCategory).HasDatabaseName("ix_memory_error_category");
    }
}

public class PendingApprovalConfiguration : IEntityTypeConfiguration<PendingApproval>
{
    public void Configure(EntityTypeBuilder<PendingApproval> builder)
    {
        builder.ToTable("pending_approvals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ActionType).HasColumnName("action_type").HasMaxLength(100);
        builder.Property(a => a.RequestedBy).HasColumnName("requested_by").HasMaxLength(128);
        builder.Property(a => a.Details).HasColumnName("details").HasColumnType("text");
        builder.Property(a => a.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(a => a.ApprovedBy).HasColumnName("approved_by").HasMaxLength(128);
        builder.Property(a => a.RejectReason).HasColumnName("reject_reason").HasColumnType("text");
        builder.Property(a => a.ContextJson).HasColumnName("context").HasColumnType("jsonb");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.ResolvedAt).HasColumnName("resolved_at");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_pending_approvals_status");
    }
}

public class AgentPoolStateConfiguration : IEntityTypeConfiguration<AgentPoolState>
{
    public void Configure(EntityTypeBuilder<AgentPoolState> builder)
    {
        builder.ToTable("agent_pool");
        builder.HasKey(p => p.AgentName);
        builder.Property(p => p.AgentName).HasColumnName("agent_name").HasMaxLength(128);
        builder.Property(p => p.AvailableSlots).HasColumnName("available_slots");
        builder.Property(p => p.TotalSlots).HasColumnName("total_slots");
        builder.Property(p => p.IsEnabled).HasColumnName("is_enabled");
        builder.Property(p => p.LastHeartbeat).HasColumnName("last_heartbeat");
        builder.Property(p => p.CircuitState)
            .HasColumnName("circuit_state")
            .HasMaxLength(16)
            .HasConversion<string>();
    }
}
