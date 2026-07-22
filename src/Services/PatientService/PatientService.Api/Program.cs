using System.Security.Cryptography.X509Certificates;
using His.Hope.Infrastructure.Caching;
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
using His.Hope.IntegrationEvents.Patient;
using His.Hope.PatientService.Api.GrpcServices;
using His.Hope.PatientService.Api.Middleware;
using His.Hope.PatientService.Application;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Infrastructure;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.PatientService.Infrastructure.Projections;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithProperty("service", "patient-service"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPatientApplication();
builder.Services.AddPatientInfrastructure(builder.Configuration);

// SECURITY: JWT Bearer authentication + permission-based authorization
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "patient-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

// TEMP: Replace Redis cache with a no-op cache to avoid StackExchange.Redis hang
// TODO: Fix StackExchange.Redis async timeout properly
builder.Services.AddSingleton<ICacheService>(new NoOpCacheService());
builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<PatientDbContext>();

// Resilient HTTP client for outbound calls to internal services
builder.Services.AddHttpClient("resilient-internal-client")
    .AddHttpMessageHandler(sp =>
    {
        var factory = sp.GetRequiredService<IResiliencePipelineFactory>();
        return new GrpcResilienceHandler(factory.GetPipeline("patient-http-internal"));
    });

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddGrpcHealthChecks();

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_patient";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
    options.ClientCertificatePath = builder.Configuration["EventBus:ClientCertificatePath"];
    options.ClientCertificatePassword = builder.Configuration["EventBus:ClientCertificatePassword"];
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PatientDbContext>(name: "patient-db", tags: ["database"])
    .AddRabbitMQCheck(
        builder.Configuration.GetValue("EventBus:HostName", "localhost")!,
        builder.Configuration.GetValue("EventBus:Port", 5672),
        builder.Configuration.GetValue("EventBus:UserName", "admin")!,
        builder.Configuration.GetValue("EventBus:Password", "admin")!,
        name: "rabbitmq", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddRedisCheck(
        builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

// Kestrel Configuration - HTTPS disabled for Docker dev; enable with cert in production
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5002, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5008, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5013, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.ListenAnyIP(5006, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Auto-create databases and subscribe to integration events on startup
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    // Ensure write-side database exists
    var writeDb = sp.GetRequiredService<His.Hope.PatientService.Infrastructure.Persistence.PatientDbContext>();
    writeDb.Database.EnsureCreated();

    // Ensure read-side database exists
    var readDb = sp.GetRequiredService<PatientReadDbContext>();
    readDb.Database.EnsureCreated();

    // Subscribe to integration events for CQRS read projections
    var eventBus = sp.GetRequiredService<IEventBus>();
    eventBus.SubscribeAsync<PatientRegisteredIntegrationEvent, PatientProjector>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<PatientUpdatedIntegrationEvent, PatientProjector>().GetAwaiter().GetResult();
}

// Middleware Pipeline (order matters)
app.UseSecurityHeaders();
// app.UseRateLimiting(); // TEMP DISABLED for debugging auth timeout
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

// Patient Endpoints (all require JWT authorization)
var patients = app.MapGroup("/api/v1/patients").RequireAuthorization();

patients.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null) =>
{
    var cacheKey = $"patients:search:{search}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPatientsQuery(search ?? "", page, pageSize), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:patients.view").WithOpenApi();

patients.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var patient = await cache.GetOrSetAsync(
        $"patient:{id}",
        async () => await mediator.Send(new GetPatientByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return patient is null ? Results.NotFound() : Results.Ok(patient);
}).RequireAuthorization("Permission:patients.view").WithOpenApi();

patients.MapGet("/search", async (
    string q, int page, int pageSize,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"patients:search:{q}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPatientsQuery(q, page, pageSize), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:patients.view").WithOpenApi();

patients.MapPost("/", async (
    CreatePatientRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new CreatePatientCommand(
        request.FirstName, request.LastName, request.MiddleName,
        request.DateOfBirth, request.GenderCode,
        request.Phone, request.Email,
        request.Street, request.District, request.City,
        request.Province, request.PostalCode, request.Country,
        request.InsuranceId, request.NationalId);

    var patient = await mediator.Send(command, ct);

    await cache.RemoveByPrefixAsync("patients:search:", ct);

    return Results.Created($"/api/v1/patients/{patient.Id}", patient);
}).RequireAuthorization("Permission:patients.create").WithOpenApi();

patients.MapPut("/{id:guid}", async (
    Guid id,
    UpdatePatientRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new UpdatePatientCommand(
        id, request.FirstName, request.LastName, request.MiddleName,
        request.DateOfBirth, request.GenderCode,
        request.Phone, request.Email,
        request.Street, request.District, request.City,
        request.Province, request.PostalCode, request.Country);

    var patient = await mediator.Send(command, ct);

    await cache.RemoveAsync($"patient:{id}", ct);
    await cache.RemoveByPrefixAsync("patients:search:", ct);

    return Results.Ok(patient);
}).RequireAuthorization("Permission:patients.update").WithOpenApi();

patients.MapPatch("/{id:guid}/deactivate", async (
    Guid id, IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new DeactivatePatientCommand(id), ct);
    await cache.RemoveAsync($"patient:{id}", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:patients.delete").WithOpenApi();

patients.MapPatch("/{id:guid}/reactivate", async (
    Guid id, IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new ReactivatePatientCommand(id), ct);
    await cache.RemoveAsync($"patient:{id}", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:patients.update").WithOpenApi();

// gRPC - allow anonymous for health checks
app.MapGrpcService<PatientGrpcServiceImpl>();
app.MapGrpcHealthChecksService().AllowAnonymous();

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
        "CN=his-hope-patient, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("patientservice");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return cert;
}

// TEMP: No-op cache — bypasses Redis until StackExchange.Redis timeout is fixed
file sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => factory();
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}



