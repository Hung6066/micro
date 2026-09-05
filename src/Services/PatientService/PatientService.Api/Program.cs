using His.Hope.AspNetCore;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using His.Hope.Infrastructure.Caching;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Audit;
using His.Hope.Persistence;
using His.Hope.IntegrationEvents.Patient;
using His.Hope.PatientService.Api.GrpcServices;
using His.Hope.PatientService.Api.Middleware;
using His.Hope.PatientService.Application;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.PatientService.Infrastructure;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.PatientService.Infrastructure.Projections;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;
using His.Hope.SharedKernel.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
                .Destructure.With<His.Hope.Infrastructure.Logging.PhiDestructuringPolicy>()
                .Enrich.WithProperty("service", "patient-service"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPatientApplication();
builder.Services.AddPatientInfrastructure(builder.Configuration);
builder.Services.AddHisHopeMigrationRunner<PatientDbContext>();

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "patient-service");

builder.Services.AddOutbox<PatientDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddGrpcHealthChecks();

builder.Services.AddHisHopeLegacyRabbitMqEventBus(builder.Configuration);

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PatientDbContext>(name: "patient-db", tags: ["database"])
    .AddRabbitMQCheck(
        builder.Configuration.GetValue("EventBus:HostName", "localhost")!,
        builder.Configuration.GetValue("EventBus:Port", 5672),
        builder.Configuration.GetValue("EventBus:UserName", "admin")!,
        His.Hope.Infrastructure.Messaging.EventBusSecurity.GetPassword(builder.Configuration),
        name: "rabbitmq", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddRedisCheck(
        builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        configuration: builder.Configuration);

// Kestrel Configuration - HTTPS disabled for Docker dev; enable with cert in production
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Any, 5002, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.Listen(System.Net.IPAddress.Any, 5013, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.Listen(System.Net.IPAddress.Any, 5006, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Subscribe to integration events and apply only development convenience schema
// creation. Production schema is owned by the EF migration history.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    var writeDb = sp.GetRequiredService<His.Hope.PatientService.Infrastructure.Persistence.PatientDbContext>();
    var readDb = sp.GetRequiredService<PatientReadDbContext>();

    if (builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
             builder.Configuration.GetValue("Persistence:MigrationOnly", false))
    {
        await sp.GetRequiredService<IMigrationRunner>().MigrateAsync();
        await readDb.Database.MigrateAsync();
    }
    else if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "PatientService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
    }

    if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
    {
        return;
    }

    // Subscribe to integration events for CQRS read projections
    var eventBus = sp.GetRequiredService<IEventBus>();
    eventBus.SubscribeAsync<PatientRegisteredIntegrationEvent, PatientProjector>().GetAwaiter().GetResult();
    eventBus.SubscribeAsync<PatientUpdatedIntegrationEvent, PatientProjector>().GetAwaiter().GetResult();
}

// Middleware Pipeline (order matters)
app.UseHisHopeServiceDefaults();
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
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();
app.UsePhiAudit();

// Patient Endpoints (all require JWT authorization)
var patients = app.MapGroup("/api/v1/patients").RequireAuthorization();

patients.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"patients:search:{scopeKey}:{search}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPatientsQuery(search ?? "", page, pageSize, accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsView).WithOpenApi();

patients.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    PatientDbContext db,
    IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!await IsPatientResourceAuthorizedAsync(id, HisHopePermissions.Patients.View, db, authorization, httpContext, ct))
        return Results.NotFound();

    var patient = await cache.GetOrSetAsync(
        $"patient:{id}",
        async () => await mediator.Send(new GetPatientByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return patient is null ? Results.NotFound() : Results.Ok(patient);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsView).WithOpenApi();

patients.MapGet("/search", async (
    string q, int page, int pageSize,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"patients:search:{scopeKey}:{q}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPatientsQuery(q, page, pageSize, accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsView).WithOpenApi();

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
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsCreate).WithOpenApi();

patients.MapPut("/{id:guid}", async (
    Guid id,
    UpdatePatientRequest request,
    IMediator mediator,
    ICacheService cache,
    PatientDbContext db,
    IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!await IsPatientResourceAuthorizedAsync(id, HisHopePermissions.Patients.Update, db, authorization, httpContext, ct))
        return Results.NotFound();

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
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsUpdate).WithOpenApi();

patients.MapPatch("/{id:guid}/deactivate", async (
    Guid id, IMediator mediator, ICacheService cache, PatientDbContext db,
    IResourceAuthorizationEvaluator authorization, HttpContext httpContext, CancellationToken ct) =>
{
    if (!await IsPatientResourceAuthorizedAsync(id, HisHopePermissions.Patients.Delete, db, authorization, httpContext, ct))
        return Results.NotFound();

    await mediator.Send(new DeactivatePatientCommand(id), ct);
    await cache.RemoveAsync($"patient:{id}", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsDelete).WithOpenApi();

patients.MapPatch("/{id:guid}/reactivate", async (
    Guid id, IMediator mediator, ICacheService cache, PatientDbContext db,
    IResourceAuthorizationEvaluator authorization, HttpContext httpContext, CancellationToken ct) =>
{
    if (!await IsPatientResourceAuthorizedAsync(id, HisHopePermissions.Patients.Update, db, authorization, httpContext, ct))
        return Results.NotFound();

    await mediator.Send(new ReactivatePatientCommand(id), ct);
    await cache.RemoveAsync($"patient:{id}", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PatientsUpdate).WithOpenApi();

// gRPC - allow anonymous for health checks
app.MapGrpcService<PatientGrpcServiceImpl>();
app.MapGrpcHealthChecksService().AllowAnonymous();

// Health checks
app.MapHealthChecks("/health/details", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
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
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, response);
    }
}).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapHisHopeHealthEndpoints();
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

static async Task<bool> IsPatientResourceAuthorizedAsync(
    Guid patientId,
    string action,
    PatientDbContext db,
    IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken cancellationToken)
{
    var patient = await db.Patients
        .AsNoTracking()
        .Where(candidate => candidate.Id == PatientId.From(patientId))
        .Select(candidate => new { candidate.FacilityId })
        .SingleOrDefaultAsync(cancellationToken);

    if (patient is null)
        return false;

    var decision = await authorization.EvaluateAsync(
        new AuthorizationContext(
            httpContext.User,
            action,
            new AuthorizationResource("patient", patientId.ToString("D"), FacilityId: patient.FacilityId),
            RequireResource: true),
        cancellationToken);

    return decision.Allowed;
}

