using System.Text;
using System.Text.Json;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using His.Hope.Contracts.Commerce;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Saga;
using His.Hope.ManufacturingService.Infrastructure.Saga;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Xunit;

[Collection("ManufacturingIntegration")]
public sealed class CommerceOrderRabbitMqTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("manufacturingrabbit")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private readonly DotNet.Testcontainers.Containers.IContainer rabbit = new ContainerBuilder()
        .WithImage("rabbitmq:3-alpine")
        .WithPortBinding(5672, assignRandomHostPort: true)
        .WithEnvironment("RABBITMQ_DEFAULT_USER", "testuser")
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", "testpass123!")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
        .WithCleanUp(true)
        .Build();

    private ManufacturingDbContext db = null!;
    private ServiceProvider services = null!;
    private CancellationTokenSource consumerCancellation = null!;
    private int rabbitPort;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await rabbit.StartAsync();
        rabbitPort = rabbit.GetMappedPublicPort(5672);
        await WaitForRabbitHandshakeAsync();

        var factory = new TestDbContextFactory(postgres.GetConnectionString());
        db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Lots.Add(new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-rabbit",
            Sku = "FG-RABBIT",
            Quantity = 20,
            Uom = "kg",
            Disposition = "Released",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Consumers:CommerceOrdersEnabled"] = "true",
                ["EventBus:HostName"] = "localhost",
                ["EventBus:Port"] = rabbitPort.ToString(),
                ["EventBus:UserName"] = "testuser",
                ["EventBus:Password"] = "testpass123!",
            })
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddDebug());
        serviceCollection.AddSingleton<IDbContextFactory<ManufacturingDbContext>>(factory);
        serviceCollection.AddSingleton<ManufacturingReservationStore>();
        serviceCollection.AddSingleton<ISagaStateStore, InMemorySagaStateStore>();
        serviceCollection.AddSingleton<ILockManager, InMemoryLockManager>();
        serviceCollection.AddSagaOptions(configuration);
        serviceCollection.AddSingleton<ISagaStep<CommerceOrderFulfillmentSagaData>, CommerceOrderFulfillmentSagaStep>();
        serviceCollection.AddSagaOrchestrator<CommerceOrderFulfillmentSagaData>();
        services = serviceCollection.BuildServiceProvider();

        var consumer = new CommerceOrderConsumer(
            services.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            services.GetRequiredService<ILogger<CommerceOrderConsumer>>());
        consumerCancellation = new CancellationTokenSource();
        await consumer.StartAsync(consumerCancellation.Token);
    }

    public async Task DisposeAsync()
    {
        if (consumerCancellation is not null)
        {
            consumerCancellation.Cancel();
            consumerCancellation.Dispose();
        }
        if (services is not null)
            await services.DisposeAsync();
        if (db is not null)
            await db.DisposeAsync();
        await rabbit.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Publishes_order_to_rabbit_and_allocates_duplicate_idempotently()
    {
        var orderId = Guid.NewGuid();
        Publish(new CommerceOrderPlacedV1(
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            orderId,
            "tenant-rabbit",
            "buyer-rabbit",
            50,
            [new CommerceOrderLineV1(Guid.NewGuid().ToString(), "FG-RABBIT", 5, 10)]));

        await EventuallyAsync(async () =>
        {
            await using var check = await new TestDbContextFactory(postgres.GetConnectionString()).CreateDbContextAsync();
            return await check.LotReservations.CountAsync(x => x.ReferenceId == orderId) == 1 &&
                await check.EventReceipts.CountAsync(x => x.AggregateId == orderId.ToString()) == 1;
        });

        Publish(new CommerceOrderPlacedV1(
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            orderId,
            "tenant-rabbit",
            "buyer-rabbit",
            50,
            [new CommerceOrderLineV1(Guid.NewGuid().ToString(), "FG-RABBIT", 5, 10)]));
        await Task.Delay(500);

        await using var final = await new TestDbContextFactory(postgres.GetConnectionString()).CreateDbContextAsync();
        (await final.LotReservations.CountAsync(x => x.ReferenceId == orderId)).Should().Be(1);
        (await final.EventReceipts.CountAsync(x => x.AggregateId == orderId.ToString())).Should().Be(1);
    }

    private void Publish(CommerceOrderPlacedV1 order)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = rabbitPort,
            UserName = "testuser",
            Password = "testpass123!",
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("his-hope.manufacturing", ExchangeType.Topic, durable: true, autoDelete: false);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = "Commerce.OrderPlaced.v1";
        channel.BasicPublish(
            "his-hope.manufacturing",
            "Commerce.OrderPlaced.v1",
            properties,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order)));
    }

    private async Task WaitForRabbitHandshakeAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = rabbitPort,
            UserName = "testuser",
            Password = "testpass123!",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(2),
        };

        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var connection = factory.CreateConnection();
                return;
            }
            catch (Exception error) when (error is SocketException or RabbitMQ.Client.Exceptions.BrokerUnreachableException)
            {
                lastError = error;
                await Task.Delay(500);
            }
        }

        throw new TimeoutException("RabbitMQ did not complete its AMQP handshake before the test fixture timeout.", lastError);
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (await condition())
                return;
            await Task.Delay(250);
        }
        throw new Xunit.Sdk.XunitException("Condition was not satisfied before timeout.");
    }

    private sealed class TestDbContextFactory(string connectionString)
        : IDbContextFactory<ManufacturingDbContext>
    {
        public ManufacturingDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new ManufacturingDbContext(options);
        }

        public Task<ManufacturingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class InMemorySagaStateStore : ISagaStateStore
    {
        private readonly Dictionary<Guid, SagaInstance> states = [];

        public Task SaveAsync(SagaInstance instance, CancellationToken ct = default)
        {
            states[instance.SagaId] = instance;
            return Task.CompletedTask;
        }

        public Task<SagaInstance?> LoadAsync(Guid sagaId, CancellationToken ct = default) =>
            Task.FromResult(states.TryGetValue(sagaId, out var state) ? state : null);

        public Task UpdateStatusAsync(Guid sagaId, string status, int stepIndex, DateTime heartbeat, CancellationToken ct = default)
        {
            var state = states[sagaId];
            state.Status = status;
            if (stepIndex >= 0) state.StepIndex = stepIndex;
            state.LastHeartbeat = heartbeat;
            state.UpdatedAt = heartbeat;
            state.Version++;
            return Task.CompletedTask;
        }

        public Task<List<SagaInstance>> GetStaleAsync(TimeSpan staleThreshold, CancellationToken ct = default) =>
            Task.FromResult(states.Values.Where(x => x.LastHeartbeat < DateTime.UtcNow - staleThreshold).ToList());
    }

    private sealed class InMemoryLockManager : ILockManager
    {
        public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default) =>
            Task.FromResult<IDistributedLock?>(new LockHandle(key));

        private sealed class LockHandle(string key) : IDistributedLock
        {
            public string Key { get; } = key;
            public long FencingToken => 1;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            public Task ReleaseAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default) => Task.FromResult(true);
        }
    }
}
