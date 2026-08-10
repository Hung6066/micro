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

        public bool IsAutoFixable => Value >= 0.6m;

    public static ConfidenceScore FromWeightedSignals(params (decimal score, decimal weight)[] signals)
    {
        if (signals.Length == 0) return new ConfidenceScore(0m);

        var totalScore = 0m;
        var totalWeight = 0m;

        foreach (var (score, weight) in signals)
        {
            totalScore += score * weight;
            totalWeight += weight;
        }

        if (totalWeight == 0) return new ConfidenceScore(0m);

        return new ConfidenceScore(totalScore / totalWeight);
    }

    public override string ToString() => $"{Value:P}";
}
