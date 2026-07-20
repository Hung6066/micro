using System.Security.Cryptography.X509Certificates;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Security.Authorization;
using His.Hope.Infrastructure.Audit;
using His.Hope.IntegrationEvents.Lab;
using His.Hope.LabService.Api.GrpcServices;
using His.Hope.LabService.Api.Endpoints;
using His.Hope.LabService.Api.Hubs;
using His.Hope.LabService.Api.Services;
using His.Hope.LabService.Api.Middleware;
using His.Hope.LabService.Application;
using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;
using His.Hope.LabService.Application.UseCases.LabOrders.Queries;
using His.Hope.LabService.Infrastructure;
using His.Hope.LabService.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLabApplication();
builder.Services.AddLabInfrastructure(builder.Configuration);

// SECURITY: Add JWT Bearer authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "lab-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

// TEMP: Replace Redis cache with a no-op cache to avoid StackExchange.Redis hang
builder.Services.AddSingleton<ICacheService>(new NoOpCacheService());

builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<LabDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddSignalR();
builder.Services.AddScoped<ICriticalAlertRealtimePublisher, CriticalAlertRealtimePublisher>();

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_lab";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
    options.ClientCertificatePath = builder.Configuration["EventBus:ClientCertificatePath"];
    options.ClientCertificatePassword = builder.Configuration["EventBus:ClientCertificatePassword"];
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LabDbContext>(name: "lab-db", tags: ["database"])
    .AddRabbitMQCheck(
        builder.Configuration.GetValue("EventBus:HostName", "localhost")!,
        builder.Configuration.GetValue("EventBus:Port", 5672),
        builder.Configuration.GetValue("EventBus:UserName", "admin")!,
        builder.Configuration.GetValue("EventBus:Password", "admin")!,
        name: "rabbitmq", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddRedisCheck(
        builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

// Kestrel Configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5010, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5017, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5018, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5019, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.ListenAnyIP(5020, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LabDbContext>();
    db.Database.EnsureCreated();
}

// Middleware Pipeline (order matters)
app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseRouting();

// SECURITY: Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();


app.UsePhiAudit();

// Lab Order Endpoints (all require JWT authorization with specific permissions)
var labOrders = app.MapGroup("/api/v1/lab-orders").RequireAuthorization();

labOrders.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    Guid? patientId = null,
    string? status = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    var cacheKey = $"laborders:search:{search}:{page}:{pageSize}:{patientId}:{status}:{dateFrom}:{dateTo}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchLabOrdersQuery(
            search ?? "", page, pageSize, patientId, status, dateFrom, dateTo), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:lab.view").WithOpenApi();

labOrders.MapGet("/search", async (
    string? q,
    int page,
    int pageSize,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct,
    Guid? patientId = null,
    string? status = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    var cacheKey = $"laborders:search:{q}:{page}:{pageSize}:{patientId}:{status}:{dateFrom}:{dateTo}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchLabOrdersQuery(
            q ?? "", page, pageSize, patientId, status, dateFrom, dateTo), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:lab.view").WithOpenApi();

labOrders.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetLabOrderByIdQuery(id), ct);
    if (result is null) return Results.NotFound();
    var labOrder = await cache.GetOrSetAsync(
        $"laborder:{id}",
        () => Task.FromResult(result),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(labOrder);
}).RequireAuthorization("Permission:lab.view").WithOpenApi();

labOrders.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"laborders:patient:{patientId}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new GetLabOrdersByPatientQuery(patientId), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:lab.view").WithOpenApi();

labOrders.MapPost("/", async (
    CreateLabOrderRequest request,
    IMediator mediator,
    ICacheService cache,
    IEventBus eventBus,
    CancellationToken ct) =>
{
    var command = new CreateLabOrderCommand(
        request.PatientId, request.ProviderId, request.EncounterId,
        request.PriorityCode, request.Notes,
        request.Tests.Select(t => new TestItem(t.TestCode, t.TestName, t.SpecimenType)).ToList());

    var labOrder = await mediator.Send(command, ct);

    await eventBus.PublishAsync(new LabOrderCreatedIntegrationEvent(
        labOrder.Id, labOrder.PatientId, labOrder.ProviderId), ct);

    await cache.RemoveByPrefixAsync("laborders:", ct);

    return Results.Created($"/api/v1/lab-orders/{labOrder.Id}", labOrder);
}).RequireAuthorization("Permission:lab.create").WithOpenApi();

labOrders.MapPut("/{id:guid}/submit", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    IEventBus eventBus,
    CancellationToken ct) =>
{
    await mediator.Send(new SubmitLabOrderCommand(id), ct);

    await eventBus.PublishAsync(new LabOrderSubmittedIntegrationEvent(id), ct);

    await cache.RemoveAsync($"laborder:{id}", ct);
    await cache.RemoveByPrefixAsync("laborders:", ct);

    return Results.NoContent();
}).RequireAuthorization("Permission:lab.update").WithOpenApi();

labOrders.MapPut("/{id:guid}/collect", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CollectLabOrderSpecimenCommand(id), ct);
    await cache.RemoveAsync($"laborder:{id}", ct);
    await cache.RemoveByPrefixAsync("laborders:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:lab.update").WithOpenApi();

labOrders.MapPut("/{id:guid}/result", async (
    Guid id,
    RecordLabResultRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new RecordLabOrderResultCommand(
        id, request.TestId, request.Value, request.AbnormalFlagCode, request.Notes), ct);
    await cache.RemoveAsync($"laborder:{id}", ct);
    await cache.RemoveByPrefixAsync("laborders:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:lab.result").WithOpenApi();

labOrders.MapPut("/{id:guid}/cancel", async (
    Guid id,
    CancelLabOrderRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CancelLabOrderCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"laborder:{id}", ct);
    await cache.RemoveByPrefixAsync("laborders:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:lab.cancel").WithOpenApi();

// Patient-specific lab-orders aggregate endpoint (routed via YARP from /api/v1/patients/{patientId:guid}/lab-orders)
app.MapGet("/api/v1/patients/{patientId:guid}/lab-orders", async (Guid patientId) =>
{
    return Results.Ok(new { patientId, items = new List<object>() });
}).RequireAuthorization("Permission:lab.view").WithOpenApi();

// gRPC
app.MapGrpcService<LabGrpcServiceImpl>();
app.MapGrpcHealthChecksService();
app.MapHub<LabCriticalAlertHub>("/hubs/lab-critical-alerts").RequireAuthorization("Permission:lab.view");
app.MapCriticalAlertEndpoints();

// Health checks
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                tags = e.Value.Tags,
                error = e.Value.Exception?.Message,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, response);
    }
}).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

static X509Certificate2 LoadServerCertificate(IConfiguration config)
{
    var certPath = config["Certificates:Server:Path"];
    var certPassword = config["Certificates:Server:Password"];
    if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword))
        return new X509Certificate2(certPath, certPassword);
    var pfxPath = Path.Combine(AppContext.BaseDirectory, "server.pfx");
    if (File.Exists(pfxPath))
        return new X509Certificate2(pfxPath, "his-hope-dev");
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
        "CN=his-hope-lab, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("labservice");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return cert;
}

// Request Records
public record CreateLabOrderRequest(
    Guid PatientId,
    Guid ProviderId,
    Guid? EncounterId,
    string PriorityCode,
    string? Notes,
    IReadOnlyList<CreateTestItemRequest> Tests);

public record CreateTestItemRequest(
    string TestCode,
    string TestName,
    string? SpecimenType);

public record RecordLabResultRequest(
    Guid TestId,
    string Value,
    string? AbnormalFlagCode,
    string? Notes);

public record CancelLabOrderRequest(string Reason);

file sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => factory();
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}


