public sealed class ManufacturingLifecycleAutomationWorker(
    ManufacturingLifecycleAutomation automation,
    IConfiguration configuration,
    ILogger<ManufacturingLifecycleAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Manufacturing:Automation:Enabled", true))
        {
            logger.LogInformation("Manufacturing lifecycle automation is disabled by configuration.");
            return;
        }

        var intervalSeconds = Math.Clamp(configuration.GetValue("Manufacturing:Automation:IntervalSeconds", 300), 30, 86_400);
        var batchSize = Math.Clamp(configuration.GetValue("Manufacturing:Automation:BatchSize", 100), 1, 1_000);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        do
        {
            try
            {
                var summary = await automation.RunOnceAsync(DateTimeOffset.UtcNow, batchSize, stoppingToken);
                if (summary != new ManufacturingAutomationRunSummary(0, 0, 0, 0, 0, 0, 0))
                    logger.LogInformation("Manufacturing lifecycle automation completed: {@Summary}", summary);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Manufacturing lifecycle automation cycle failed; it will be retried.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
