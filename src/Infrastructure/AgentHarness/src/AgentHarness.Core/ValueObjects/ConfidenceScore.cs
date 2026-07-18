namespace His.Hope.AgentHarness.Core.ValueObjects;

public readonly struct ConfidenceScore : IEquatable<ConfidenceScore>
{
    public decimal Value { get; }

    public ConfidenceScore(decimal value)
    {
        if (value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence score must be between 0 and 1.");
        Value = value;
    }

    public bool Equals(ConfidenceScore other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ConfidenceScore other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(ConfidenceScore left, ConfidenceScore right) => left.Equals(right);

    public static bool operator !=(ConfidenceScore left, ConfidenceScore right) => !left.Equals(right);

    public override string ToString() => $"{Value:P}";
}
