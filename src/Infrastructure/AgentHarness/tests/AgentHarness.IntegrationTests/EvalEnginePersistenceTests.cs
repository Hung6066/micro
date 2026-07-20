using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Infrastructure.Persistence;

namespace His.Hope.AgentHarness.IntegrationTests;

/// <summary>
/// NOTE on full StateStore persistence testing:
/// The real HarnessDbContext uses pgvector (Vector type and HasPostgresExtension),
/// which is not supported by EF Core InMemory or SQLite providers. The StateStore 
/// tests below use a TestHarnessDbContext (extends HarnessDbContext via SQLite 
/// in-memory) that only includes EvalSuite and EvalRun configurations, avoiding 
/// the pgvector dependency. The real production StateStore methods are exercised.
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
    /// Lightweight HarnessDbContext subclass that only maps EvalSuite and EvalRun,
    /// avoiding the pgvector dependency from MemoryEntry config. Ignores all other
    /// entity types discovered through inherited DbSet properties.
    /// </summary>
    private class TestHarnessDbContext : HarnessDbContext
    {
        public TestHarnessDbContext(DbContextOptions<HarnessDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("harness");
            modelBuilder.ApplyConfiguration(new EvalSuiteConfiguration());
            modelBuilder.ApplyConfiguration(new EvalRunConfiguration());

            // Ignore all other entities to prevent convention-based discovery
            // via DbSet properties inherited from HarnessDbContext
            modelBuilder.Ignore<PipelineRun>();
            modelBuilder.Ignore<AgentRun>();
            modelBuilder.Ignore<QualityGate>();
            modelBuilder.Ignore<Artifact>();
            modelBuilder.Ignore<AgentPoolState>();
            modelBuilder.Ignore<PipelineCheckpoint>();
            modelBuilder.Ignore<MemoryEntry>();
            modelBuilder.Ignore<PendingApproval>();
        }
    }



    private static HarnessDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<HarnessDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var ctx = new TestHarnessDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task StateStore_SaveAndGetEvalSuite_ShouldPersistAndReload()
    {
        // Arrange
        using var ctx = CreateTestDbContext();
        var store = new StateStore(ctx);
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
        using var ctx = CreateTestDbContext();
        var store = new StateStore(ctx);

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
        using var ctx = CreateTestDbContext();
        var store = new StateStore(ctx);

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
        using var ctx = CreateTestDbContext();
        var store = new StateStore(ctx);

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
        using var ctx = CreateTestDbContext();
        var store = new StateStore(ctx);

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
