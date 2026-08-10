using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace His.Hope.DatabaseContinuityService;

public sealed class ContinuityScheduler(
    ContinuityJobStore store,
    IOptions<DatabaseContinuityOptions> options,
    ILogger<ContinuityScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = options.Value;
                if (config.Enabled && config.SchedulerEnabled &&
                    await store.AcquireSchedulerLockAsync(Environment.MachineName, TimeSpan.FromMinutes(2)))
                {
                    await ScheduleIfDueAsync("backup", config.BackupIntervalHours, "production", null);
                    await ScheduleIfDueAsync("restore-drill", config.RestoreDrillIntervalHours,
                        config.RestoreDrillTargetEnvironment, DateTimeOffset.UtcNow);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Database continuity scheduler cycle failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        async Task ScheduleIfDueAsync(string operation, int intervalHours, string targetEnvironment,
            DateTimeOffset? restorePoint)
        {
            if (intervalHours <= 0) return;
            var last = await store.GetLastScheduledAtAsync(operation);
            if (last is not null && last.Value.AddHours(intervalHours) > DateTimeOffset.UtcNow) return;

            var job = new ContinuityJob
            {
                Operation = operation,
                TargetEnvironment = targetEnvironment,
                RestorePoint = restorePoint,
                ActorSubject = "scheduler"
            };
            await store.EnqueueAsync(job, stoppingToken);
            await store.MarkScheduledAsync(operation, DateTimeOffset.UtcNow);
            logger.LogInformation("Scheduled database continuity job {Operation} {JobId}", operation, job.JobId);
        }
    }
}

public sealed class ContinuityWorker(
    ContinuityJobStore store,
    ContinuityExecutor executor,
    ContinuityAuditStore audit,
    IOptions<DatabaseContinuityOptions> options,
    ILogger<ContinuityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = $"continuity-{Environment.MachineName}-{Guid.NewGuid():N}";
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await store.ReadAsync(consumer, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }
                var job = await store.GetAsync(message.Value.JobId, stoppingToken);
                if (job is null) { await store.AckAsync(message.Value.MessageId); continue; }
                try
                {
                    job.Attempt++;
                    job.Status = ContinuityJobStatus.Running;
                    await store.SaveAsync(job, stoppingToken);
                    await audit.UpsertAsync(job, stoppingToken);
                    await executor.ExecuteAsync(job, stoppingToken);
                    if (job.Status == ContinuityJobStatus.Failed)
                        await store.RetryAsync(job, job.ErrorCode ?? "continuity_job_failed", options.Value.MaxAttempts, stoppingToken);
                    await audit.UpsertAsync(job, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await store.RetryAsync(job, "continuity_worker_failed", options.Value.MaxAttempts, stoppingToken);
                    await audit.UpsertAsync(job, stoppingToken);
                    logger.LogError(ex, "Database continuity job {JobId} failed", job.JobId);
                }
                finally { await store.AckAsync(message.Value.MessageId); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Database continuity worker dependency unavailable; retrying");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

public sealed class ContinuityExecutor(
    IOptions<DatabaseContinuityOptions> options,
    ContinuityJobStore store,
    ContinuityAuditStore audit,
    BackupStorageCoordinator storage,
    ILogger<ContinuityExecutor> logger)
{
    public async Task ExecuteAsync(ContinuityJob job, CancellationToken ct)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ExecutorPath))
        {
            job.Status = ContinuityJobStatus.Failed;
            job.ErrorCode = "continuity_executor_not_configured";
            await store.SaveAsync(job, ct);
            if (job.Status == ContinuityJobStatus.Completed)
                await store.MarkCompletedAsync(job.Operation, job.UpdatedAt);
            await audit.UpsertAsync(job, ct);
            return;
        }
        if (!Path.IsPathFullyQualified(config.ExecutorPath))
        {
            job.Status = ContinuityJobStatus.Failed;
            job.ErrorCode = "continuity_executor_path_invalid";
            await store.SaveAsync(job, ct);
            if (job.Status == ContinuityJobStatus.Completed)
                await store.MarkCompletedAsync(job.Operation, job.UpdatedAt);
            await audit.UpsertAsync(job, ct);
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = config.ExecutorPath,
                WorkingDirectory = string.IsNullOrWhiteSpace(config.ExecutorWorkingDirectory)
                    ? Path.GetDirectoryName(config.ExecutorPath) ?? AppContext.BaseDirectory
                    : config.ExecutorWorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("--operation");
        process.StartInfo.ArgumentList.Add(job.Operation);
        process.StartInfo.ArgumentList.Add("--target-environment");
        process.StartInfo.ArgumentList.Add(job.TargetEnvironment);
        if (job.RestorePoint is not null)
        {
            process.StartInfo.ArgumentList.Add("--restore-point");
            process.StartInfo.ArgumentList.Add(job.RestorePoint.Value.ToString("O"));
        }

        var started = DateTimeOffset.UtcNow;
        var storageResults = new List<StorageSyncResult>();
        try
        {
            if (job.Operation == "restore-drill")
            {
                foreach (var database in GetDatabases())
                    storageResults.Add(await storage.PrepareRestoreAsync(database, ct));
            }
            if (!process.Start()) throw new InvalidOperationException("Executor did not start.");
            await process.WaitForExitAsync(ct);
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            job.Status = process.ExitCode == 0 ? ContinuityJobStatus.Completed : ContinuityJobStatus.Failed;
            if (process.ExitCode == 0 && job.Operation == "backup")
            {
                var files = ParseBackupFiles(output);
                if (files.Count > 0) storageResults.Add(await storage.PersistBackupAsync(files, ct));
            }
            RetentionCleanupResult? retention = null;
            if (process.ExitCode == 0 && job.Operation == "backup")
                retention = await storage.CleanupExpiredAsync(ct);
            var fallbackUsed = storageResults.Any(x => x.UsedFallback);
            job.ErrorCode = process.ExitCode == 0
                ? (fallbackUsed ? "storage_fallback_local" : null)
                : "continuity_executor_failed";
            job.ResultJson = JsonSerializer.Serialize(new
            {
                exitCode = process.ExitCode,
                startedAt = started,
                completedAt = DateTimeOffset.UtcNow,
                durationSeconds = (DateTimeOffset.UtcNow - started).TotalSeconds,
                output = Redact(output),
                error = Redact(error),
                storage = storageResults,
                retention
            });
            await store.SaveAsync(job, ct);
            if (job.Status == ContinuityJobStatus.Completed)
                await store.MarkCompletedAsync(job.Operation, job.UpdatedAt);
            await audit.UpsertAsync(job, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database continuity executor failed for {JobId}", job.JobId);
            job.Status = ContinuityJobStatus.Failed;
            job.ErrorCode = "continuity_executor_exception";
            await store.SaveAsync(job, ct);
            await audit.UpsertAsync(job, ct);
        }
    }

    private static IReadOnlyCollection<string> ParseBackupFiles(string output) =>
        Regex.Matches(output, @"^Backup created:\s*(?<path>.+)$", RegexOptions.Multiline)
            .Select(match => match.Groups["path"].Value.Trim())
            .Where(File.Exists)
            .SelectMany(path => new[] { path, $"{path}.manifest.json" })
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyCollection<string> GetDatabases() =>
        (Environment.GetEnvironmentVariable("DATABASE_CONTINUITY_DATABASES") ?? "identitydb,patientdb,appointmentdb,clinicaldb,labdb,billingdb,pharmacydb")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var builder = new StringBuilder();
        foreach (var line in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("token", StringComparison.OrdinalIgnoreCase))
                builder.AppendLine("[redacted]");
            else builder.AppendLine(line.Length > 2000 ? line[..2000] : line);
        }
        return builder.ToString();
    }
}
