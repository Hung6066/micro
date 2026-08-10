using Microsoft.Extensions.DependencyInjection;
using Temporalio.Extensions.Hosting;
using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Infrastructure.Temporal;

public static class TemporalDependencyInjection
{
    public static IServiceCollection AddTemporalInfrastructure(
        this IServiceCollection services,
        string temporalServerUrl,
        bool useTemporal)
    {
        if (!useTemporal)
            return services;

        services.AddTemporalClient(options =>
        {
            options.TargetHost = temporalServerUrl;
            options.Namespace = "default";
        });

        services.AddSingleton<IPipelineEngine, TemporalPipelineEngine>();

        return services;
    }

    public static IServiceCollection AddTemporalWorker(
        this IServiceCollection services,
        string temporalServerUrl)
    {
        services.AddTemporalClient(options =>
        {
            options.TargetHost = temporalServerUrl;
            options.Namespace = "default";
        });

        services.AddHostedTemporalWorker(
                clientTargetHost: temporalServerUrl,
                clientNamespace: "default",
                taskQueue: "agent-harness")
            .AddScopedActivities<AgentActivities>()
            .AddWorkflow<PipelineWorkflow>();

        return services;
    }
}
