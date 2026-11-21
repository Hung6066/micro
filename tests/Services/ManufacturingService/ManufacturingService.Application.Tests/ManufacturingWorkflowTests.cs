using His.Hope.ManufacturingService.Application;

namespace ManufacturingService.Application.Tests;

public sealed class ManufacturingWorkflowRegistryTests
{
    [Fact]
    public void TryGet_returns_definition_for_p1_entity_types()
    {
        foreach (var entityType in new[] { "deviation", "capa", "quality-inspection", "recipe", "product-specification" })
        {
            var definition = ManufacturingWorkflowRegistry.TryGet(entityType);
            Assert.NotNull(definition);
            Assert.Equal(entityType, definition!.EntityType, ignoreCase: true);
            Assert.True(definition.Steps.Count >= 2);
        }
    }

    [Fact]
    public void TryGet_returns_null_for_unknown_entity_type() =>
        Assert.Null(ManufacturingWorkflowRegistry.TryGet("unknown-workflow"));
}

public sealed class ManufacturingStatusHistoryBuilderTests
{
    [Fact]
    public void ForProductionBatch_builds_created_started_completed_entries()
    {
        var createdAt = DateTimeOffset.Parse("2026-08-27T08:00:00Z");
        var startedAt = createdAt.AddHours(1);
        var completedAt = startedAt.AddHours(2);

        var history = ManufacturingStatusHistoryBuilder.ForProductionBatch(
            Guid.NewGuid(),
            "customer-factory-x",
            "Completed",
            createdAt,
            startedAt,
            completedAt);

        Assert.Equal(3, history.Count);
        Assert.Equal("Created", history[0].ToStatus);
        Assert.Equal("Started", history[1].ToStatus);
        Assert.Equal("Completed", history[2].ToStatus);
    }

    [Fact]
    public void ForDeviation_builds_requested_and_resolution_entries()
    {
        var createdAt = DateTimeOffset.Parse("2026-08-27T08:00:00Z");
        var approvedAt = createdAt.AddHours(1);

        var history = ManufacturingStatusHistoryBuilder.ForDeviation(
            Guid.NewGuid(),
            "customer-factory-x",
            "Approved",
            "operator-a",
            createdAt,
            "supervisor-b",
            approvedAt,
            null);

        Assert.Equal(2, history.Count);
        Assert.Equal("Requested", history[0].ToStatus);
        Assert.Equal("Approved", history[1].ToStatus);
        Assert.Equal("supervisor-b", history[1].Actor);
    }
}
