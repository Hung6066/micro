using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Infrastructure.Persistence;

namespace His.Hope.AgentHarness.IntegrationTests;

/// <summary>
/// NOTE on full StateStore persistence testing:
/// The real HarnessDbContext uses pgvector (Vector type and HasPostgresExtension),
/// which is not supported by EF Core InMemory or SQLite providers. The StateStore 
/// tests below use a lightweight EvalOnlyDbContext (SQLite in-memory) that only 
/// includes EvalSuite and EvalRun configurations, avoiding the pgvector dependency.
///
/// Full migration/table validation (including FK constraint, index creation, 
/// and schema migrations) requires a CockroachDB or PostgreSQL instance, 
/// which is not available in this CI environment.
/// </summary>
public class EvalEnginePersistenceTests
{
    // ============================================================
    // Unit-level model behavior tests
    // ============================================================

    [Fact]
    public void EvalSuite_CreateAndProperties_ShouldWork()
    {
        // Arrange & Act
        var suite = EvalSuite.Create("test-suite", "coding", "A test suite", """{"key":"value"}""");

        // Assert
        suite.Should().NotBeNull();
        suite.Id.Should().NotBeEmpty();
        suite.Name.Should().Be("test-suite");
        suite.Domain.Should().Be("coding");
        suite.Description.Should().Be("A test suite");
        suite.DefinitionJson.Should().Be("""{"key":"value"}""");
        suite.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EvalRun_CreateAndComplete_ShouldTrackMetrics()
    {
        // Arrange
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");

        // Act
        var run = EvalRun.Create(suite.Id, "dotnet", "gpt-4");

        // Assert initial state
        run.Id.Should().NotBeEmpty();
        run.EvalSuiteId.Should().Be(suite.Id);
        run.TargetAgent.Should().Be("dotnet");
        run.TargetModel.Should().Be("gpt-4");
        run.Status.Should().Be(EvalRunStatus.Pending);

        // Act - complete
        run.Complete(0.85, 0.95, 90, """{"results":"ok"}""");

        // Assert completed state
        run.Status.Should().Be(EvalRunStatus.Completed);
        run.PassAt1.Should().Be(0.85);
        run.PassAtK.Should().Be(0.95);
        run.JudgeScore.Should().Be(90);
        run.RawResultJson.Should().Be("""{"results":"ok"}""");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void EvalRun_Fail_ShouldTransitionToFailed()
    {
        // Arrange
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");
        var run = EvalRun.Create(suite.Id, "dotnet");

        // Act
        run.Fail();

        // Assert
        run.Status.Should().Be(EvalRunStatus.Failed);
        run.CompletedAt.Should().NotBeNull();
        run.PassAt1.Should().BeNull();
    }

    [Fact]
    public void EvalRun_WithoutModel_ShouldAllowNullTargetModel()
    {
        // Arrange & Act
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");
        var run = EvalRun.Create(suite.Id, "angular");

        // Assert
        run.TargetModel.Should().BeNull();
        run.TargetAgent.Should().Be("angular");
    }

    // ============================================================
    // StateStore persistence tests (via SQlite in-memory, eval-only)
    // ============================================================

    /// <summary>
    /// Lightweight DbContext that only maps EvalSuite and EvalRun,
    /// avoiding the pgvector dependency from MemoryEntry config.
    /// </summary>
    private class EvalOnlyDbContext : DbContext
    {
        public DbSet<EvalSuite> EvalSuites => Set<EvalSuite>();
        public DbSet<EvalRun> EvalRuns => Set<EvalRun>();

        public EvalOnlyDbContext(DbContextOptions<EvalOnlyDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EvalSuite>(ConfigureEvalSuite);
            modelBuilder.Entity<EvalRun>(ConfigureEvalRun);
        }

        private static void ConfigureEvalSuite(EntityTypeBuilder<EvalSuite> builder)
        {
            builder.ToTable("eval_suites");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(256);
            builder.Property(e => e.Domain).HasColumnName("domain").HasMaxLength(128);
            builder.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
            builder.Property(e => e.DefinitionJson).HasColumnName("definition_json");
            builder.Property(e => e.CreatedAt).HasColumnName("created_at");
            builder.HasIndex(e => e.Name).HasDatabaseName("ix_eval_suites_name").IsUnique();
        }

        private static void ConfigureEvalRun(EntityTypeBuilder<EvalRun> builder)
        {
            builder.ToTable("eval_runs");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("id");
            builder.Property(r => r.EvalSuiteId).HasColumnName("eval_suite_id");
            builder.Property(r => r.TargetAgent).HasColumnName("target_agent").HasMaxLength(128);
            builder.Property(r => r.TargetModel).HasColumnName("target_model").HasMaxLength(128);
            builder.Property(r => r.PassAt1).HasColumnName("pass_at_1");
            builder.Property(r => r.PassAtK).HasColumnName("pass_at_k");
            builder.Property(r => r.JudgeScore).HasColumnName("judge_score");
            builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
            builder.Property(r => r.StartedAt).HasColumnName("started_at");
            builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
            builder.Property(r => r.RawResultJson).HasColumnName("raw_result_json");
            builder.HasIndex(r => r.EvalSuiteId).HasDatabaseName("ix_eval_runs_suite_id");
            builder.HasIndex(r => r.TargetAgent).HasDatabaseName("ix_eval_runs_target_agent");
        }
    }

    /// <summary>
    /// Minimal StateStore implementation that uses EvalOnlyDbContext.
    /// Tests the same persistence contract as the real StateStore.
    /// </summary>
    private class EvalOnlyStateStore
    {
        private readonly EvalOnlyDbContext _db;

        public EvalOnlyStateStore(EvalOnlyDbContext db) => _db = db;

        public async Task SaveEvalSuiteAsync(EvalSuite suite, CancellationToken ct = default)
        {
            var existing = await _db.EvalSuites.FindAsync([suite.Id], ct);
            if (existing is null)
                _db.EvalSuites.Add(suite);
            else
                _db.Entry(existing).CurrentValues.SetValues(suite);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<EvalSuite?> GetEvalSuiteAsync(string name, CancellationToken ct = default)
            => await _db.EvalSuites.AsNoTracking().FirstOrDefaultAsync(e => e.Name == name, ct);

        public async Task<List<EvalSuite>> GetEvalSuitesAsync(CancellationToken ct = default)
            => await _db.EvalSuites.AsNoTracking().ToListAsync(ct);

        public async Task SaveEvalRunAsync(EvalRun run, CancellationToken ct = default)
        {
            var existing = await _db.EvalRuns.FindAsync([run.Id], ct);
            if (existing is null)
                _db.EvalRuns.Add(run);
            else
                _db.Entry(existing).CurrentValues.SetValues(run);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<EvalRun?> GetEvalRunAsync(Guid id, CancellationToken ct = default)
            => await _db.EvalRuns.FindAsync([id], ct);

        public async Task<List<EvalRun>> GetEvalRunsAsync(Guid evalSuiteId, CancellationToken ct = default)
            => await _db.EvalRuns.AsNoTracking()
                .Where(r => r.EvalSuiteId == evalSuiteId)
                .OrderByDescending(r => r.StartedAt)
                .ToListAsync(ct);
    }

    private static EvalOnlyDbContext CreateEvalDbContext()
    {
        var options = new DbContextOptionsBuilder<EvalOnlyDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var ctx = new EvalOnlyDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task StateStore_SaveAndGetEvalSuite_ShouldPersistAndReload()
    {
        // Arrange
        using var ctx = CreateEvalDbContext();
        var store = new EvalOnlyStateStore(ctx);
        var suite = EvalSuite.Create("persist-suite", "coding", "Persist test", """{"tasks":[{"input":"hello","expected":"world"}]}""");

        // Act - persist
        await store.SaveEvalSuiteAsync(suite);

        // Act - reload by name
        var reloaded = await store.GetEvalSuiteAsync("persist-suite");

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(suite.Id);
        reloaded.Name.Should().Be("persist-suite");
        reloaded.Domain.Should().Be("coding");
        reloaded.Description.Should().Be("Persist test");
        reloaded.DefinitionJson.Should().Be("""{"tasks":[{"input":"hello","expected":"world"}]}""");
        reloaded.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StateStore_SaveAndGetEvalRun_ShouldPersistAndReload()
    {
        // Arrange
        using var ctx = CreateEvalDbContext();
        var store = new EvalOnlyStateStore(ctx);

        var suite = EvalSuite.Create("run-persist", "qa", "Run persist", """{"tasks":[{"input":"x"}]}""");
        await store.SaveEvalSuiteAsync(suite);

        var run = EvalRun.Create(suite.Id, "dotnet", "gpt-4");
        run.Complete(0.75, 0.90, 85, """[true,false,true]""");

        // Act
        await store.SaveEvalRunAsync(run);

        // Act - reload by id
        var reloaded = await store.GetEvalRunAsync(run.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(run.Id);
        reloaded.EvalSuiteId.Should().Be(suite.Id);
        reloaded.TargetAgent.Should().Be("dotnet");
        reloaded.TargetModel.Should().Be("gpt-4");
        reloaded.PassAt1.Should().Be(0.75);
        reloaded.PassAtK.Should().Be(0.90);
        reloaded.JudgeScore.Should().Be(85);
        reloaded.Status.Should().Be(EvalRunStatus.Completed);
        reloaded.RawResultJson.Should().Be("[true,false,true]");
        reloaded.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StateStore_SaveEvalRun_UpdateExisting_ShouldOverwrite()
    {
        // Arrange
        using var ctx = CreateEvalDbContext();
        var store = new EvalOnlyStateStore(ctx);

        var suite = EvalSuite.Create("update-test", "qa", "Update test", """{"tasks":[{"input":"x"}]}""");
        await store.SaveEvalSuiteAsync(suite);

        var run = EvalRun.Create(suite.Id, "dotnet");
        await store.SaveEvalRunAsync(run);

        // Act - update with completion
        run.Complete(0.5, 0.8, 70, """[true,false]""");
        await store.SaveEvalRunAsync(run);

        // Assert
        var reloaded = await store.GetEvalRunAsync(run.Id);
        reloaded.Should().NotBeNull();
        reloaded!.PassAt1.Should().Be(0.5);
        reloaded.PassAtK.Should().Be(0.8);
        reloaded.JudgeScore.Should().Be(70);
        reloaded.Status.Should().Be(EvalRunStatus.Completed);
    }

    [Fact]
    public async Task StateStore_GetEvalRunsBySuiteId_ShouldReturnOrderedResults()
    {
        // Arrange
        using var ctx = CreateEvalDbContext();
        var store = new EvalOnlyStateStore(ctx);

        var suite = EvalSuite.Create("list-test", "qa", "List test", """{"tasks":[{"input":"x"}]}""");
        await store.SaveEvalSuiteAsync(suite);

        var run1 = EvalRun.Create(suite.Id, "dotnet", "gpt-4");
        run1.Complete(0.9, 1.0, 95, "[]");
        await store.SaveEvalRunAsync(run1);

        await Task.Delay(10); // ensure different timestamps

        var run2 = EvalRun.Create(suite.Id, "dotnet", "claude-3");
        run2.Complete(0.8, 0.9, 85, "[]");
        await store.SaveEvalRunAsync(run2);

        // Act
        var runs = await store.GetEvalRunsAsync(suite.Id);

        // Assert - ordered by StartedAt descending (most recent first)
        runs.Should().HaveCount(2);
        runs[0].Id.Should().Be(run2.Id);
        runs[1].Id.Should().Be(run1.Id);
    }

    [Fact]
    public async Task StateStore_GetEvalSuites_ShouldReturnAll()
    {
        // Arrange
        using var ctx = CreateEvalDbContext();
        var store = new EvalOnlyStateStore(ctx);

        var suite1 = EvalSuite.Create("s1", "d1", "desc1", "{}");
        var suite2 = EvalSuite.Create("s2", "d2", "desc2", "{}");
        await store.SaveEvalSuiteAsync(suite1);
        await store.SaveEvalSuiteAsync(suite2);

        // Act
        var suites = await store.GetEvalSuitesAsync();

        // Assert
        suites.Should().HaveCount(2);
        suites.Select(s => s.Name).Should().Contain(new[] { "s1", "s2" });
    }
}
