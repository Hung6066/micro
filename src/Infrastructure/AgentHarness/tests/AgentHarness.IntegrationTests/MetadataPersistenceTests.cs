using System.Text.Json;
using FluentAssertions;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.IntegrationTests;

/// <summary>
/// Tests that PipelineRun metadata (including adaptive risk metadata)
/// is correctly stored, serialized, and round-tripped.
/// </summary>
public class MetadataPersistenceTests
{
    [Fact]
    public void PipelineRun_AddMetadata_ShouldStoreValues()
    {
        // Arrange
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);

        // Act — add metadata that mirrors what StoreRiskMetadataAsync would set
        run.AddMetadata("adaptive_risk_checked_at", DateTime.UtcNow.ToString("O"));
        run.AddMetadata("adaptive_risk_0", "level=Low;score=0.15;Agent 'dotnet' adequate");
        run.AddMetadata("adaptive_risk_1", "level=High;score=0.72;low AIS (20.0)");
        run.AddMetadata("adaptive_risk_count", "2");

        // Assert — all keys accessible on the model
        run.Metadata.Should().ContainKey("adaptive_risk_checked_at");
        run.Metadata.Should().ContainKey("adaptive_risk_0");
        run.Metadata.Should().ContainKey("adaptive_risk_1");
        run.Metadata.Should().ContainKey("adaptive_risk_count");
        run.Metadata["adaptive_risk_count"].Should().Be("2");
    }

    [Fact]
    public void PipelineRun_Metadata_ShouldSurviveSerializationRoundtrip()
    {
        // Arrange — simulate EF Core serializing Metadata to JSONB and back
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);

        run.AddMetadata("adaptive_risk_checked_at", "2025-01-01T00:00:00Z");
        run.AddMetadata("adaptive_risk_0", "level=Low;score=0.15;adequate");
        run.AddMetadata("adaptive_risk_count", "1");

        var options = new JsonSerializerOptions();

        // Act — serialize Metadata (as EF Core would for JSONB column)
        var serialized = JsonSerializer.Serialize(run.Metadata, options);
        serialized.Should().NotBeNullOrEmpty();

        // Act — deserialize back (as EF Core would when reading from DB)
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(serialized, options);

        // Assert — all keys and values preserved through roundtrip
        deserialized.Should().NotBeNull();
        deserialized!["adaptive_risk_checked_at"].Should().Be("2025-01-01T00:00:00Z");
        deserialized["adaptive_risk_0"].Should().Be("level=Low;score=0.15;adequate");
        deserialized["adaptive_risk_count"].Should().Be("1");
    }

    [Fact]
    public void PipelineRun_MetadataMutation_ChangesDictionaryContent()
    {
        // Arrange
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);

        // Act — simulate StoreRiskMetadataAsync mutation pattern
        run.AddMetadata("step", "1");
        run.Metadata.Count.Should().Be(1);

        run.AddMetadata("adaptive_risk_checked_at", DateTime.UtcNow.ToString("O"));
        run.Metadata.Count.Should().Be(2);

        run.AddMetadata("adaptive_risk_count", "2");
        run.Metadata.Count.Should().Be(3);
    }

    [Fact]
    public void PipelineRun_Metadata_ShouldHandleEmptyMetadata()
    {
        // Arrange
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");

        // Assert — default Metadata should be empty, not null
        run.Metadata.Should().NotBeNull("Metadata should never be null");
        run.Metadata.Should().BeEmpty("newly created runs should have no metadata");
    }
}
