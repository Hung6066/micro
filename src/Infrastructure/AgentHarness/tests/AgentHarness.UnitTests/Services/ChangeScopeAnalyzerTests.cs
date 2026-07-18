using FluentAssertions;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class ChangeScopeAnalyzerTests
{
    [Fact]
    public void Analyze_BackendChange_ShouldTriggerDotnetAndDba()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Services/PatientService/PatientService.Api/Program.cs" });
        scope.TriggeredAgents.Should().Contain("dotnet");
        scope.TriggeredAgents.Should().Contain("dba");
        scope.PhasesToSkip.Should().NotContain(PipelinePhase.Implement);
    }

    [Fact]
    public void Analyze_FrontendOnly_ShouldTriggerAngularOnly()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Frontend/src/app/app.component.ts" });
        scope.TriggeredAgents.Should().Contain("angular");
        scope.TriggeredAgents.Should().NotContain("dotnet");
    }

    [Fact]
    public void Analyze_DocsOnly_ShouldSkipImplementAndTest()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "docs/architecture.md" });
        scope.PhasesToSkip.Should().Contain(PipelinePhase.Implement);
        scope.PhasesToSkip.Should().Contain(PipelinePhase.Test);
    }
}
