using FluentAssertions;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class ConditionalDagBuilderTests
{
    [Fact]
    public void Build_WithFullScope_ShouldCreateAllPhases()
    {
        var scope = new ChangeScope { TriggeredAgents = new() { "dotnet", "angular", "dba", "devops" }, PhasesToSkip = new() };
        var builder = new ConditionalDagBuilder();
        var dag = builder.Build(scope);
        dag.Nodes.Should().HaveCountGreaterThan(3);
        dag.GetPhaseNodes(PipelinePhase.Plan).Should().HaveCount(1);
        dag.GetPhaseNodes(PipelinePhase.Commit).Should().HaveCount(1);
    }

    [Fact]
    public void Build_DocsOnly_ShouldSkipImplementAndTest()
    {
        var scope = new ChangeScope { TriggeredAgents = new() { "docs" }, PhasesToSkip = new() { PipelinePhase.Implement, PipelinePhase.Test } };
        var builder = new ConditionalDagBuilder();
        var dag = builder.Build(scope);
        dag.GetPhaseNodes(PipelinePhase.Implement).Should().BeEmpty();
        dag.GetPhaseNodes(PipelinePhase.Test).Should().BeEmpty();
    }
}
