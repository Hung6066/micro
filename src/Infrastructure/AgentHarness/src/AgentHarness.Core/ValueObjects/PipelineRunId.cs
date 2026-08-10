namespace His.Hope.AgentHarness.Core.ValueObjects;

public readonly struct PipelineRunId : IEquatable<PipelineRunId>
{
    public Guid Value { get; }

    public PipelineRunId(Guid value) => Value = value;

    public static PipelineRunId New() => new(Guid.NewGuid());

    public bool Equals(PipelineRunId other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is PipelineRunId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(PipelineRunId left, PipelineRunId right) => left.Equals(right);

    public static bool operator !=(PipelineRunId left, PipelineRunId right) => !left.Equals(right);

    public override string ToString() => Value.ToString();
}
