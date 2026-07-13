using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.HealthChecks;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddRabbitMQCheck(
        this IHealthChecksBuilder builder,
        string hostName = "localhost",
        int port = 5672,
        string? userName = null,
        string? password = null,
        string name = "rabbitmq",
        HealthStatus? failureStatus = null)
    {
        return builder.AddCheck(name, () =>
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName ?? "guest",
                    Password = password ?? "guest",
                    RequestedHeartbeat = TimeSpan.FromSeconds(5),
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                return HealthCheckResult.Healthy("RabbitMQ is reachable");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("RabbitMQ is not reachable", ex);
            }
        }, failureStatus ?? HealthStatus.Degraded,
        ["messaging", "rabbitmq"]);
    }

    public static IHealthChecksBuilder AddRedisCheck(
        this IHealthChecksBuilder builder,
        string connectionString = "localhost:6379",
        string name = "redis",
        HealthStatus? failureStatus = null)
    {
        return builder.AddCheck(name, () =>
        {
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(connectionString);
                var db = multiplexer.GetDatabase();
                db.Ping();
                multiplexer.Close();

                return HealthCheckResult.Healthy("Redis is reachable");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis is not reachable", ex);
            }
        }, failureStatus ?? HealthStatus.Degraded,
        ["cache", "redis"]);
    }

    public static IHealthChecksBuilder AddGrpcServiceCheck(
        this IHealthChecksBuilder builder,
        string serviceName,
        string endpoint,
        HealthStatus? failureStatus = null)
    {
        return builder.AddCheck($"grpc-{serviceName}", () =>
        {
            try
            {
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(endpoint);
                var client = new Grpc.Health.V1.Health.HealthClient(channel);
                var response = client.Check(new Grpc.Health.V1.HealthCheckRequest
                {
                    Service = serviceName
                });

                return response.Status == Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Serving
                    ? HealthCheckResult.Healthy($"gRPC {serviceName} is serving")
                    : HealthCheckResult.Unhealthy($"gRPC {serviceName} is not serving");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"gRPC {serviceName} is not reachable", ex);
            }
        }, failureStatus ?? HealthStatus.Degraded,
        ["grpc", serviceName]);
    }
}
