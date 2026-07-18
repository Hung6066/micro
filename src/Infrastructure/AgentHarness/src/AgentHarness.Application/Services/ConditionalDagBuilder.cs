namespace His.Hope.AgentHarness.Application.Services;

public class ConditionalDagBuilder
{
    public PipelineDag Build(ChangeScope scope)
    {
        var dag = new PipelineDag();
        var planNode = dag.AddNode("plan", PipelinePhase.Plan);

        // Phase 2: Implement — only triggered agents (skipped if excluded by scope analysis)
        if (!scope.PhasesToSkip.Contains(PipelinePhase.Implement))
        {
            foreach (var agent in scope.TriggeredAgents)
            {
                var implNode = dag.AddNode(agent, PipelinePhase.Implement);
                dag.AddEdge(planNode, implNode);
            }
        }

        // Phase 3: Test — only if implementations happened
        if (!scope.PhasesToSkip.Contains(PipelinePhase.Test))
        {
            if (scope.TriggeredAgents.Contains("dotnet"))
            {
                var testNode = dag.AddNode("testing-backend", PipelinePhase.Test);
                dag.AddEdge(dag.Nodes.First(n => n.AgentName == "dotnet"), testNode);
            }
            if (scope.TriggeredAgents.Contains("angular"))
            {
                var testNode = dag.AddNode("testing-frontend", PipelinePhase.Test);
                dag.AddEdge(dag.Nodes.First(n => n.AgentName == "angular"), testNode);
            }
        }

        // Phase 4: Validate
        var validateNode = dag.AddNode("validate", PipelinePhase.Validate);
        foreach (var node in dag.GetPhaseNodes(PipelinePhase.Test))
            dag.AddEdge(node, validateNode);

        // Loop back edges
        foreach (var gateNode in dag.GetGateNodes())
            dag.AddLoopBackEdge(gateNode, planNode, maxIterations: 3, viaAgent: "loop-engineer");

        // Phase 5: Commit
        var commitNode = dag.AddNode("commit", PipelinePhase.Commit);
        dag.AddEdge(validateNode, commitNode);

        return dag;
    }
}
