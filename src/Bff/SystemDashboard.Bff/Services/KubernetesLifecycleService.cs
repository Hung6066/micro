using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SystemDashboard.Bff.Services;

public sealed class KubernetesLifecycleService : IServiceLifecycleService
{
    private readonly string _namespace;
    private readonly ILogger<KubernetesLifecycleService> _logger;

    public KubernetesLifecycleService(ILogger<KubernetesLifecycleService> logger)
    {
        _namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "his-hope";
        _logger = logger;
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectlAsync("scale", $"deployment/{serviceName} --replicas=1", ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectlAsync("scale", $"deployment/{serviceName} --replicas=0", ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunKubectlAsync("rollout", $"restart deployment/{serviceName}", ct);
    }

    private async Task<bool> RunKubectlAsync(string command, string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = $"-n \"{_namespace}\" {command} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "kubectl {Command} for {Service} failed (exit {ExitCode}): {Error}",
                    command, args, process.ExitCode, stderr);
                return false;
            }

            _logger.LogInformation(
                "kubectl {Command} for {Service} succeeded: {Output}",
                command, args, stdout);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "kubectl {Command} for {Service} threw an exception",
                command, args);
            return false;
        }
    }
}
