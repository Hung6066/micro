using Grpc.Net.Client;
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
        return builder.AddTypeActivatedCheck<RabbitMQHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            ["messaging", "rabbitmq"],
            hostName, port, userName ?? "guest", password ?? "guest");
    }

    public static IHealthChecksBuilder AddRedisCheck(
        this IHealthChecksBuilder builder,
        string connectionString = "localhost:6379",
        string name = "redis",
        HealthStatus? failureStatus = null)
    {
        return builder.AddTypeActivatedCheck<RedisHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Degraded,
            ["cache", "redis"],
            connectionString);
    }

    public static IHealthChecksBuilder AddGrpcServiceCheck(
        this IHealthChecksBuilder builder,
        string serviceName,
        string endpoint,
        HealthStatus? failureStatus = null)
    {
        return builder.AddTypeActivatedCheck<GrpcHealthCheck>(
            name: $"grpc-{serviceName}",
            failureStatus ?? HealthStatus.Degraded,
            ["grpc", serviceName],
            serviceName, endpoint);
    }

    private class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;

        public RabbitMQHealthCheck(string hostName, int port, string userName, string password)
        {
            _hostName = hostName;
            _port = port;
            _userName = userName;
            _password = password;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _hostName,
                    Port = _port,
                    UserName = _userName,
                    Password = _password,
                    RequestedHeartbeat = TimeSpan.FromSeconds(5),
                };
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ is reachable"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ is not reachable", ex));
            }
        }
    }

    private class RedisHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;

        public RedisHealthCheck(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(_connectionString);
                var db = multiplexer.GetDatabase();
                db.Ping();
                multiplexer.Close();
                return Task.FromResult(HealthCheckResult.Healthy("Redis is reachable"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Redis is not reachable", ex));
            }
        }
    }

    private class GrpcHealthCheck : IHealthCheck
    {
        private readonly string _serviceName;
        private readonly string _endpoint;

        public GrpcHealthCheck(string serviceName, string endpoint)
        {
            _serviceName = serviceName;
            _endpoint = endpoint;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var channel = GrpcChannel.ForAddress(_endpoint);
                var client = new Grpc.Health.V1.Health.HealthClient(channel);
                
                // Try with the specific service name first
                Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus status;
                try
                {
                    var response = await client.CheckAsync(
                        new Grpc.Health.V1.HealthCheckRequest { Service = _serviceName },
                        deadline: DateTime.UtcNow.AddSeconds(5),
                        cancellationToken: cancellationToken);
                    status = response.Status;
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
                {
                    // Service name not registered - check overall server health
                    status = Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown;
                }

                if (status == Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Serving)
                    return HealthCheckResult.Healthy($"gRPC service '{_serviceName}' is healthy");

                // Fallback: query overall server health with empty service name
                try
                {
                    var overallResponse = await client.CheckAsync(
                        new Grpc.Health.V1.HealthCheckRequest(),
                        deadline: DateTime.UtcNow.AddSeconds(5),
                        cancellationToken: cancellationToken);

                    return overallResponse.Status == Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Serving
                        ? HealthCheckResult.Healthy($"gRPC server at '{_endpoint}' is healthy")
                        : HealthCheckResult.Unhealthy($"gRPC server at '{_endpoint}' status: {overallResponse.Status}");
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
                {
                    return HealthCheckResult.Healthy($"gRPC server at '{_endpoint}' is reachable (health check service not registered)");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"gRPC service '{_serviceName}' is not reachable", ex);
            }
        }
    }
}
