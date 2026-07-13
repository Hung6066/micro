using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Saga;

public interface ISagaStep<TData>
{
    Task ExecuteAsync(TData data, CancellationToken ct = default);
    Task CompensateAsync(TData data, CancellationToken ct = default);
}

public class SagaOrchestrator<TData>
{
    private readonly List<ISagaStep<TData>> _steps = [];
    private readonly ILogger<SagaOrchestrator<TData>> _logger;
    private readonly Stack<int> _executedSteps = new();

    public SagaOrchestrator(ILogger<SagaOrchestrator<TData>> logger) =>
        _logger = logger;

    public SagaOrchestrator<TData> AddStep(ISagaStep<TData> step)
    {
        _steps.Add(step);
        return this;
    }

    public async Task ExecuteAsync(TData data, CancellationToken ct = default)
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            try
            {
                _logger.LogInformation("Executing saga step {Step}/{Total}: {StepType}",
                    i + 1, _steps.Count, _steps[i].GetType().Name);

                await _steps[i].ExecuteAsync(data, ct);
                _executedSteps.Push(i);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga step {Step}/{Total} failed: {StepType}",
                    i + 1, _steps.Count, _steps[i].GetType().Name);

                await CompensateAsync(data, ct);
                throw new SagaExecutionException(
                    $"Saga failed at step {i + 1}/{_steps.Count}", ex);
            }
        }

        _logger.LogInformation("Saga completed successfully");
    }

    private async Task CompensateAsync(TData data, CancellationToken ct)
    {
        _logger.LogWarning("Starting saga compensation for {Count} steps",
            _executedSteps.Count);

        while (_executedSteps.Count > 0)
        {
            var stepIndex = _executedSteps.Pop();
            try
            {
                _logger.LogInformation("Compensating step {Step}: {StepType}",
                    stepIndex + 1, _steps[stepIndex].GetType().Name);

                await _steps[stepIndex].CompensateAsync(data, ct);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Compensation failed for step {Step}",
                    stepIndex + 1);
            }
        }
    }
}

public class SagaExecutionException : Exception
{
    public SagaExecutionException(string message, Exception inner)
        : base(message, inner) { }
}
