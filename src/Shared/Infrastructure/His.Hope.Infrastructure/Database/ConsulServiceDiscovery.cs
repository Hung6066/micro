using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Database;

public static class ConsulServiceDiscovery
{
    public static IServiceCollection AddConsulRegistration(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceId,
        int port)
    {
        services.AddSingleton<IConsulClient>(_ => new ConsulClient(cfg =>
        {
            cfg.Address = new Uri(configuration
                .GetValue("Consul:Host", "http://localhost:8500")!);
        }));

        services.AddHostedService(sp =>
        {
            var consul = sp.GetRequiredService<IConsulClient>();
            var logger = sp.GetRequiredService<ILogger<ConsulHostedService>>();
            return new ConsulHostedService(consul, logger, serviceName, serviceId,
                configuration.GetValue<string>("Consul:Host", "localhost")!, port);
        });

        return services;
    }
}

public class ConsulHostedService : IHostedService
{
    private readonly IConsulClient _consul;
    private readonly ILogger<ConsulHostedService> _logger;
    private readonly string _serviceName;
    private readonly string _serviceId;
    private readonly string _host;
    private readonly int _port;
    private string? _registrationId;

    public ConsulHostedService(
        IConsulClient consul,
        ILogger<ConsulHostedService> logger,
        string serviceName,
        string serviceId,
        string host,
        int port)
    {
        _consul = consul;
        _logger = logger;
        _serviceName = serviceName;
        _serviceId = serviceId;
        _host = host;
        _port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationId = $"{_serviceName}-{_serviceId}-{_port}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _serviceName,
            Address = _host,
            Port = _port,
            Tags = ["his-hope", _serviceName],
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_host}:{_port}/health",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(5),
            },
        };

        await _consul.Agent.ServiceRegister(registration, cancellationToken);
        _logger.LogInformation("Registered service {Name} with Consul as {Id}",
            _serviceName, _registrationId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registrationId is not null)
        {
            await _consul.Agent.ServiceDeregister(_registrationId, cancellationToken);
            _logger.LogInformation("Deregistered service {Id} from Consul", _registrationId);
        }
    }
}
