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
using His.Hope.IntegrationEvents.Pharmacy;
using His.Hope.PharmacyService.Api.GrpcServices;
using His.Hope.PharmacyService.Api.Middleware;
using His.Hope.PharmacyService.Application;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Application.UseCases.Medications.Queries;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Infrastructure;
using His.Hope.PharmacyService.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPharmacyApplication();
builder.Services.AddPharmacyInfrastructure(builder.Configuration);

// SECURITY: Add JWT Bearer authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "pharmacy-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<PharmacyDbContext>();

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
    options.ExchangeName = "his_hope_pharmacy";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
    options.ClientCertificatePath = builder.Configuration["EventBus:ClientCertificatePath"];
    options.ClientCertificatePassword = builder.Configuration["EventBus:ClientCertificatePassword"];
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PharmacyDbContext>(name: "pharmacy-db", tags: ["database"])
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
    options.ListenAnyIP(5011, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5030, listenOptions =>
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
    });
});

var app = builder.Build();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
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

// SECURITY: Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();


app.UsePhiAudit();

// Medication Endpoints (all require JWT authorization with specific permissions)
var medications = app.MapGroup("/api/v1/medications").RequireAuthorization();

medications.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    string? category = null) =>
{
    var cacheKey = $"medications:search:{search}:{page}:{pageSize}:{category}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchMedicationsQuery(
            search ?? "", page, pageSize, category), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

medications.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var medication = await cache.GetOrSetAsync(
        $"medication:{id}",
        async () => await mediator.Send(new GetMedicationByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return medication is null ? Results.NotFound() : Results.Ok(medication);
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

medications.MapPost("/", async (
    CreateMedicationRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new CreateMedicationCommand(
        request.Name, request.GenericName, request.BrandName,
        request.DosageForm, request.Strength, request.Route,
        request.Category, request.Manufacturer,
        request.RequiresPrescription);

    var medication = await mediator.Send(command, ct);

    await cache.RemoveByPrefixAsync("medications:", ct);

    return Results.Created($"/api/v1/medications/{medication.Id}", medication);
}).RequireAuthorization("Permission:pharmacy.create").WithOpenApi();

medications.MapPut("/{id:guid}", async (
    Guid id,
    UpdateMedicationRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new UpdateMedicationCommand(
        id, request.Name, request.GenericName, request.BrandName,
        request.DosageForm, request.Strength, request.Route,
        request.Category, request.Manufacturer,
        request.RequiresPrescription);

    var medication = await mediator.Send(command, ct);

    await cache.RemoveAsync($"medication:{id}", ct);
    await cache.RemoveByPrefixAsync("medications:", ct);

    return Results.Ok(medication);
}).RequireAuthorization("Permission:pharmacy.update").WithOpenApi();

medications.MapPut("/{id:guid}/deactivate", async (
    Guid id, IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new DeactivateMedicationCommand(id), ct);
    await cache.RemoveAsync($"medication:{id}", ct);
    await cache.RemoveByPrefixAsync("medications:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:pharmacy.update").WithOpenApi();

// Prescription Endpoints (all require JWT authorization with specific permissions)
var prescriptions = app.MapGroup("/api/v1/prescriptions").RequireAuthorization();

prescriptions.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    Guid? patientId = null,
    string? status = null) =>
{
    var cacheKey = $"prescriptions:search:{page}:{pageSize}:{patientId}:{status}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPrescriptionsQuery(
            "", page, pageSize, patientId, status), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

prescriptions.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var prescription = await cache.GetOrSetAsync(
        $"prescription:{id}",
        async () => await mediator.Send(new GetPrescriptionByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return prescription is null ? Results.NotFound() : Results.Ok(prescription);
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

prescriptions.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"prescriptions:patient:{patientId}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new GetPrescriptionsByPatientQuery(patientId), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

prescriptions.MapPost("/", async (
    CreatePrescriptionRequest request,
    IMediator mediator,
    ICacheService cache,
    IEventBus eventBus,
    CancellationToken ct) =>
{
    var firstMedication = request.Medications.FirstOrDefault()
        ?? throw new BadHttpRequestException("At least one medication is required.");

    var command = new CreatePrescriptionCommand(
        request.PatientId, request.ProviderId, firstMedication.MedicationId,
        firstMedication.MedicationName, firstMedication.Strength,
        firstMedication.DosageForm, firstMedication.DosageInstructions,
        firstMedication.Route, firstMedication.Quantity,
        firstMedication.Refills, request.Notes, null);

    var prescription = await mediator.Send(command, ct);

    await eventBus.PublishAsync(new PrescriptionCreatedIntegrationEvent(
        prescription.Id, prescription.PatientId, prescription.ProviderId,
        prescription.MedicationName, prescription.Strength,
        prescription.DosageForm, prescription.DosageInstructions,
        prescription.Quantity, prescription.Refills,
        prescription.PrescribedDate), ct);

    await cache.RemoveByPrefixAsync("prescriptions:", ct);

    return Results.Created($"/api/v1/prescriptions/{prescription.Id}", prescription);
}).RequireAuthorization("Permission:pharmacy.create").WithOpenApi();

prescriptions.MapPut("/{id:guid}/fill", async (
    Guid id, IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new FillPrescriptionCommand(id), ct);
    await cache.RemoveAsync($"prescription:{id}", ct);
    await cache.RemoveByPrefixAsync("prescriptions:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:pharmacy.dispense").WithOpenApi();

prescriptions.MapPut("/{id:guid}/cancel", async (
    Guid id, CancelPrescriptionRequest request,
    IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new CancelPrescriptionCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"prescription:{id}", ct);
    await cache.RemoveByPrefixAsync("prescriptions:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:pharmacy.cancel").WithOpenApi();

// Patient-specific prescriptions aggregate endpoint (routed via YARP from /api/v1/patients/{patientId:guid}/prescriptions)
app.MapGet("/api/v1/patients/{patientId:guid}/prescriptions", async (Guid patientId) =>
{
    return Results.Ok(new { patientId, items = new List<object>() });
}).RequireAuthorization("Permission:pharmacy.view").WithOpenApi();

// gRPC
app.MapGrpcService<PharmacyGrpcServiceImpl>();
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
        "CN=his-hope-pharmacy, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("pharmacyservice");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return cert;
}

// Request Records
public record CreateMedicationRequest(
    string Name,
    string? GenericName,
    string? BrandName,
    string DosageForm,
    string Strength,
    string? Route,
    string? Category,
    string? Manufacturer,
    bool RequiresPrescription);

public record UpdateMedicationRequest(
    string Name,
    string? GenericName,
    string? BrandName,
    string DosageForm,
    string Strength,
    string? Route,
    string? Category,
    string? Manufacturer,
    bool RequiresPrescription);

public record PrescriptionMedicationInput(
    Guid? MedicationId,
    string MedicationName,
    string Strength,
    string DosageForm,
    string DosageInstructions,
    string? Route,
    int Quantity,
    int Refills);

public record CreatePrescriptionRequest(
    Guid PatientId,
    Guid ProviderId,
    IReadOnlyList<PrescriptionMedicationInput> Medications,
    string? Notes);

public record CancelPrescriptionRequest(string Reason);


