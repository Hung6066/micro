namespace His.Hope.AgentHarness.Core.ValueObjects;

public readonly struct AgentRunId : IEquatable<AgentRunId>
{
    public Guid Value { get; }

    public AgentRunId(Guid value) => Value = value;

    public static AgentRunId New() => new(Guid.NewGuid());

    public bool Equals(AgentRunId other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is AgentRunId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(AgentRunId left, AgentRunId right) => left.Equals(right);

    public static bool operator !=(AgentRunId left, AgentRunId right) => !left.Equals(right);

    public override string ToString() => Value.ToString();
}
