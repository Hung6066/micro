using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Persistence;

public class HarnessDbContext : DbContext
{
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<QualityGate> QualityGates => Set<QualityGate>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<AgentPoolState> AgentPool => Set<AgentPoolState>();
    public DbSet<EvalSuite> EvalSuites => Set<EvalSuite>();
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();

    public HarnessDbContext(DbContextOptions<HarnessDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasDefaultSchema("harness");
        modelBuilder.ApplyConfiguration(new PipelineRunConfiguration());
        modelBuilder.ApplyConfiguration(new AgentRunConfiguration());
        modelBuilder.ApplyConfiguration(new QualityGateConfiguration());
        modelBuilder.ApplyConfiguration(new ArtifactConfiguration());
        modelBuilder.ApplyConfiguration(new AgentPoolStateConfiguration());
        modelBuilder.ApplyConfiguration(new PipelineCheckpointConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new PendingApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new EvalSuiteConfiguration());
        modelBuilder.ApplyConfiguration(new EvalRunConfiguration());
    }
}
