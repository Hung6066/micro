using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Entities;

public class CriticalAlertRule : Entity<Guid>
{
    public string TestCode { get; private set; } = null!;
    public string TestName { get; private set; } = null!;
    public string? Unit { get; private set; }
    public decimal? LowCriticalValue { get; private set; }
    public decimal? HighCriticalValue { get; private set; }
    public bool IsActive { get; private set; }
    public string CreatedByUserId { get; private set; } = null!;
    public string CreatedByDisplayName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private CriticalAlertRule(
        Guid id,
        string testCode,
        string testName,
        string? unit,
        decimal? lowCriticalValue,
        decimal? highCriticalValue,
        string createdByUserId,
        string createdByDisplayName)
        : base(id)
    {
        TestCode = Guard.Against.NullOrWhiteSpace(testCode, nameof(testCode));
        TestName = Guard.Against.NullOrWhiteSpace(testName, nameof(testName));
        Unit = unit;
        LowCriticalValue = lowCriticalValue;
        HighCriticalValue = highCriticalValue;
        ValidateThresholdRange(lowCriticalValue, highCriticalValue);
        IsActive = true;
        CreatedByUserId = Guard.Against.NullOrWhiteSpace(createdByUserId, nameof(createdByUserId));
        CreatedByDisplayName = Guard.Against.NullOrWhiteSpace(createdByDisplayName, nameof(createdByDisplayName));
        CreatedAt = DateTime.UtcNow;
    }

    public static CriticalAlertRule Create(
        string testCode,
        string testName,
        string? unit,
        decimal? lowCriticalValue,
        decimal? highCriticalValue,
        string createdByUserId,
        string createdByDisplayName)
    {
        var id = Guid.NewGuid();
        return new CriticalAlertRule(id, testCode, testName, unit, lowCriticalValue, highCriticalValue, createdByUserId, createdByDisplayName);
    }

    public void UpdateThresholds(decimal? lowCriticalValue, decimal? highCriticalValue)
    {
        ValidateThresholdRange(lowCriticalValue, highCriticalValue);

        LowCriticalValue = lowCriticalValue;
        HighCriticalValue = highCriticalValue;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string testCode, string testName)
    {
        TestCode = Guard.Against.NullOrWhiteSpace(testCode, nameof(testCode));
        TestName = Guard.Against.NullOrWhiteSpace(testName, nameof(testName));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetUnit(string? unit)
    {
        Unit = unit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool Matches(string? resultUnit, decimal resultValue)
    {
        if (!IsActive)
            return false;

        if (!string.IsNullOrWhiteSpace(Unit) && !string.Equals(Unit, resultUnit, StringComparison.OrdinalIgnoreCase))
            return false;

        var lowerMatch = LowCriticalValue.HasValue && resultValue <= LowCriticalValue.Value;
        var upperMatch = HighCriticalValue.HasValue && resultValue >= HighCriticalValue.Value;

        return lowerMatch || upperMatch;
    }

    public decimal? GetMatchedThreshold(decimal resultValue)
    {
        if (!IsActive)
            return null;

        var lowerMatch = LowCriticalValue.HasValue && resultValue <= LowCriticalValue.Value;
        if (lowerMatch)
            return LowCriticalValue;

        var upperMatch = HighCriticalValue.HasValue && resultValue >= HighCriticalValue.Value;
        if (upperMatch)
            return HighCriticalValue;

        return null;
    }

    private static void ValidateThresholdRange(decimal? lowCriticalValue, decimal? highCriticalValue)
    {
        if (lowCriticalValue.HasValue && highCriticalValue.HasValue && lowCriticalValue.Value > highCriticalValue.Value)
            throw new DomainException("Low critical value cannot exceed high critical value.");
    }

    private CriticalAlertRule() { }
}
