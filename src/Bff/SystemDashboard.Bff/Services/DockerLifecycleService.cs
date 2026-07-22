using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class DockerLifecycleService : IServiceLifecycleService
{
    private readonly string _projectName;
    private readonly ILogger<DockerLifecycleService> _logger;

    public DockerLifecycleService(IOptions<DockerOptions> dockerOptions, ILogger<DockerLifecycleService> logger)
    {
        _projectName = dockerOptions.Value.ComposeProjectName;
        _logger = logger;
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunDockerComposeAsync("start", serviceName, ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunDockerComposeAsync("stop", serviceName, ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        return await RunDockerComposeAsync("restart", serviceName, ct);
    }

    private async Task<bool> RunDockerComposeAsync(string command, string serviceName, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -p \"{_projectName}\" {command} {serviceName}",
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
                    "Docker Compose {Command} for {Service} failed (exit {ExitCode}): {Error}",
                    command, serviceName, process.ExitCode, stderr);
                return false;
            }

            _logger.LogInformation(
                "Docker Compose {Command} for {Service} succeeded: {Output}",
                command, serviceName, stdout);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Docker Compose {Command} for {Service} threw an exception",
                command, serviceName);
            return false;
        }
    }
}
