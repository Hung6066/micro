using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace His.Hope.Infrastructure.Saga;

public sealed class SagaOptions
{
    public const string SectionName = "Saga";
    public int PerStepTimeoutSeconds { get; set; } = 30;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int LockTtlSeconds { get; set; } = 300;
    public int RecoveryCheckIntervalSeconds { get; set; } = 30;
    public int RecoveryStaleThresholdSeconds { get; set; } = 60;
    public int RecoveryLockTtlSeconds { get; set; } = 300;
    public int RecoveryBatchSize { get; set; } = 100;

    public void Validate()
    {
        if (PerStepTimeoutSeconds <= 0 || HeartbeatIntervalSeconds <= 0 ||
            LockTtlSeconds <= 0 || RecoveryCheckIntervalSeconds <= 0 ||
            RecoveryStaleThresholdSeconds <= 0 || RecoveryLockTtlSeconds <= 0 ||
            RecoveryBatchSize <= 0)
            throw new OptionsValidationException(nameof(SagaOptions), typeof(SagaOptions),
                ["All Saga timing and batch options must be greater than zero."]);
    }
}
