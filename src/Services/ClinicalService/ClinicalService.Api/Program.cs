using System.Security.Cryptography.X509Certificates;
using His.Hope.ClinicalService.Api.GrpcServices;
using His.Hope.ClinicalService.Api.Middleware;
using His.Hope.ClinicalService.Application;
using His.Hope.ClinicalService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Infrastructure;
using His.Hope.ClinicalService.Infrastructure.Persistence;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Security.Authorization;
using His.Hope.Infrastructure.Audit;
using His.Hope.IntegrationEvents.Clinical;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddClinicalApplication();
builder.Services.AddClinicalInfrastructure(builder.Configuration);

// SECURITY: Add JWT Bearer authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "clinical-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!);

builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<ClinicalDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_clinical";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
    options.ClientCertificatePath = builder.Configuration["EventBus:ClientCertificatePath"];
    options.ClientCertificatePassword = builder.Configuration["EventBus:ClientCertificatePassword"];
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ClinicalDbContext>(name: "clinical-db", tags: ["database"])
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
    options.ListenAnyIP(5004, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadServerCertificate(builder.Configuration);
            httpsOptions.CheckCertificateRevocation = false;
        });
    });

    options.ListenAnyIP(5010, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5016, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.ListenAnyIP(5015, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadServerCertificate(builder.Configuration);
            httpsOptions.CheckCertificateRevocation = false;
        });
    });
});

var app = builder.Build();

// Auto-create database on startup (delegated to Infrastructure layer)
ClinicalDbInitializer.Initialize(app.Services);

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

// SECURITY: Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();


app.UsePhiAudit();

// Encounter Endpoints (all require JWT authorization with specific permissions)
var encounters = app.MapGroup("/api/v1/encounters").RequireAuthorization();

encounters.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var result = await cache.GetOrSetAsync(
        "encounters:all",
        async () => await mediator.Send(new SearchEncountersQuery("", 1, 1000), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:clinical.view").WithOpenApi();

encounters.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var encounterDto = await mediator.Send(new GetEncounterByIdQuery(id), ct);
    if (encounterDto is null) return Results.NotFound();
    var encounter = await cache.GetOrSetAsync(
        $"encounter:{id}",
        async () => encounterDto,
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(encounter);
}).RequireAuthorization("Permission:clinical.view").WithOpenApi();

encounters.MapGet("/search", async (
    string? q, int page, int pageSize,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"encounters:search:{q}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchEncountersQuery(q ?? "", page, pageSize), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:clinical.view").WithOpenApi();

encounters.MapPost("/", async (
    StartEncounterRequest request,
    IMediator mediator,
    IEventBus eventBus,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new StartEncounterCommand(
        request.PatientId, request.ProviderId, request.AppointmentId,
        request.EncounterTypeCode, null, null);

    var encounter = await mediator.Send(command, ct);

    await eventBus.PublishAsync(new EncounterStartedIntegrationEvent(
        encounter.Id, encounter.PatientId, encounter.ProviderId,
        request.AppointmentId, encounter.EncounterTypeCode, encounter.EncounterDate), ct);

    await cache.RemoveByPrefixAsync("encounters:", ct);

    return Results.Created($"/api/v1/encounters/{encounter.Id}", encounter);
}).RequireAuthorization("Permission:clinical.create").WithOpenApi();

encounters.MapPost("/{id:guid}/vitals", async (
    Guid id,
    RecordVitalsRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new RecordVitalsCommand(
        id, request.Temperature, request.HeartRate, request.RespiratoryRate,
        request.SystolicBP, request.DiastolicBP, request.OxygenSaturation,
        request.HeightCm, request.WeightKg, request.Bmi);

    var encounter = await mediator.Send(command, ct);

    await cache.RemoveAsync($"encounter:{id}", ct);
    await cache.RemoveByPrefixAsync("encounters:", ct);

    return Results.Ok(encounter);
}).RequireAuthorization("Permission:clinical.update").WithOpenApi();

encounters.MapPost("/{id:guid}/diagnosis", async (
    Guid id,
    AddDiagnosisRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new AddDiagnosisCommand(
        id, request.ConditionName, request.Icd10Code, request.IsPrimary, request.Notes);

    var encounter = await mediator.Send(command, ct);

    await cache.RemoveAsync($"encounter:{id}", ct);
    await cache.RemoveByPrefixAsync("encounters:", ct);

    return Results.Ok(encounter);
}).RequireAuthorization("Permission:clinical.update").WithOpenApi();

encounters.MapPut("/{id:guid}/complete", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CompleteEncounterCommand(id), ct);

    await cache.RemoveAsync($"encounter:{id}", ct);
    await cache.RemoveByPrefixAsync("encounters:", ct);

    return Results.NoContent();
}).RequireAuthorization("Permission:clinical.update").WithOpenApi();

encounters.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    int page,
    int pageSize,
    DateTime? fromDate,
    DateTime? toDate,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"encounters:patient:{patientId}:{page}:{pageSize}:{fromDate}:{toDate}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(
            new GetEncountersByPatientQuery(patientId, page, pageSize, fromDate, toDate), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:clinical.view").WithOpenApi();

// Dashboard Stats Endpoint - requires clinical.view permission
var dashboard = app.MapGroup("/api/v1/dashboard").RequireAuthorization();

dashboard.MapGet("/stats", async (
    ClinicalDbContext db,
    ICacheService cache,
    CancellationToken ct) =>
{
    var result = await cache.GetOrSetAsync(
        "dashboard:stats",
        async () =>
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var inProgressStatus = EncounterStatus.InProgress;

            // Aggregate counts
            var totalEncounters = await db.Encounters.CountAsync(ct);
            var activeEncounters = await db.Encounters.CountAsync(e => e.Status == inProgressStatus, ct);
            var todayEncounters = await db.Encounters.CountAsync(e => e.EncounterDate >= today, ct);

            // Encounters by type - materialize grouped key with value converter, then map names client-side
            var byTypeRaw = await db.Encounters
                .GroupBy(e => e.EncounterType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var encountersByType = byTypeRaw
                .Select(x => new { type = x.Type.Name, code = x.Type.Code, count = x.Count })
                .ToList();

            // Recent encounters - materialize entities, then project client-side
            var recentRaw = await db.Encounters
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync(ct);
            var recentEncounters = recentRaw
                .Select(e => new
                {
                    e.Id,
                    e.PatientId,
                    encounterType = e.EncounterType.Name,
                    status = e.Status.Name,
                    e.EncounterDate,
                    e.CreatedAt
                })
                .ToList();

            return new
            {
                totalEncounters,
                activeEncounters,
                todayEncounters,
                encountersByType,
                recentEncounters
            };
        },
        TimeSpan.FromMinutes(2), ct);

    return Results.Ok(result);
}).RequireAuthorization("Permission:reports.view").WithOpenApi();

// gRPC
app.MapGrpcService<ClinicalGrpcServiceImpl>();
app.MapGrpcHealthChecksService();

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
        "CN=his-hope-clinical, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("clinicalservice");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return cert;
}

// Request Records
public record StartEncounterRequest(Guid PatientId, Guid ProviderId, Guid? AppointmentId, string EncounterTypeCode);
public record RecordVitalsRequest(decimal? Temperature, int? HeartRate, int? RespiratoryRate,
    int? SystolicBP, int? DiastolicBP, decimal? OxygenSaturation, decimal? HeightCm, decimal? WeightKg, decimal? Bmi);
public record AddDiagnosisRequest(string ConditionName, string Icd10Code, bool IsPrimary, string? Notes);

