namespace His.Hope.AgentHarness.Core.Models;

public enum EvalRunStatus { Pending, Running, Completed, Failed }

public class EvalRun
{
    public Guid Id { get; private set; }
    public Guid EvalSuiteId { get; private set; }
    public string TargetAgent { get; private set; } = string.Empty;
    public string? TargetModel { get; private set; }
    public double? PassAt1 { get; private set; }
    public double? PassAtK { get; private set; }
    public int? JudgeScoreValue { get; private set; }
    public EvalRunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RawResultJson { get; private set; }

    private EvalRun() { }

    public static EvalRun Create(Guid evalSuiteId, string targetAgent, string? targetModel = null)
    {
        return new EvalRun
        {
            Id = Guid.NewGuid(),
            EvalSuiteId = evalSuiteId,
            TargetAgent = targetAgent,
            TargetModel = targetModel,
            Status = EvalRunStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Complete(double passAt1, double passAtK, int? judgeScore, string rawResultJson)
    {
        PassAt1 = passAt1;
        PassAtK = passAtK;
        JudgeScoreValue = judgeScore;
        RawResultJson = rawResultJson;
        Status = EvalRunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail()
    {
        Status = EvalRunStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }
}
