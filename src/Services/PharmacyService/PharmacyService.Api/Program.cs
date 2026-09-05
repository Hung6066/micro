using His.Hope.AspNetCore;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Audit;
using His.Hope.Persistence;
using His.Hope.IntegrationEvents.Pharmacy;
using His.Hope.PharmacyService.Api.GrpcServices;
using His.Hope.PharmacyService.Api.Middleware;
using His.Hope.PharmacyService.Application;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Application.UseCases.Medications.Queries;
using Microsoft.EntityFrameworkCore;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.PharmacyService.Infrastructure;
using His.Hope.PharmacyService.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
                .Destructure.With<His.Hope.Infrastructure.Logging.PhiDestructuringPolicy>()
                .Enrich.WithProperty("service", "pharmacy-service"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPharmacyApplication();
builder.Services.AddPharmacyInfrastructure(builder.Configuration);
builder.Services.AddHisHopeMigrationRunner<PharmacyDbContext>();

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "pharmacy-service");

builder.Services.AddOutbox<PharmacyDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddHisHopeLegacyRabbitMqEventBus(builder.Configuration);

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PharmacyDbContext>(name: "pharmacy-db", tags: ["database"])
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

// Kestrel Configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Any, 5030, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.Listen(System.Net.IPAddress.Any, 5032, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.Listen(System.Net.IPAddress.Any, 5015, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Development-only convenience for a local empty database. Production schema is
// owned by the external CockroachDB migration workflow.
if (builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
         builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    using var scope = app.Services.CreateScope();
      await scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateAsync();
}
else if (!app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "PharmacyService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
}

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
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

// Medication Endpoints (all require JWT authorization with specific permissions)
var medications = app.MapGroup("/api/v1/medications").RequireAuthorization();

medications.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    string? category = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"medications:search:{scopeKey}:{search}:{page}:{pageSize}:{category}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchMedicationsQuery(
            search ?? "", page, pageSize, category,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

medications.MapGet("/search", async (
    string? q,
    int page,
    int pageSize,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    string? category = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"medications:search:{scopeKey}:{q}:{page}:{pageSize}:{category}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchMedicationsQuery(
            q ?? "", page, pageSize, category,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

medications.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var medication = await cache.GetOrSetAsync<MedicationDto>(
        $"medication:{id}",
        async () => (await mediator.Send(new GetMedicationByIdQuery(id), ct))!,
        TimeSpan.FromMinutes(5), ct);
    return medication is null ? Results.NotFound() : Results.Ok(medication);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

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
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyCreate).WithOpenApi();

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
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyUpdate).WithOpenApi();

medications.MapPut("/{id:guid}/deactivate", async (
    Guid id, IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new DeactivateMedicationCommand(id), ct);
    await cache.RemoveAsync($"medication:{id}", ct);
    await cache.RemoveByPrefixAsync("medications:", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyUpdate).WithOpenApi();

// Prescription Endpoints (all require JWT authorization with specific permissions)
var prescriptions = app.MapGroup("/api/v1/prescriptions").RequireAuthorization();

prescriptions.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    Guid? patientId = null,
    string? status = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"prescriptions:search:{scopeKey}:{page}:{pageSize}:{patientId}:{status}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPrescriptionsQuery(
            "", page, pageSize, patientId, status,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

prescriptions.MapGet("/search", async (
    string? q,
    int page,
    int pageSize,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    Guid? patientId = null,
    string? status = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"prescriptions:search:{scopeKey}:{q}:{page}:{pageSize}:{patientId}:{status}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchPrescriptionsQuery(
            q ?? "", page, pageSize, patientId, status,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

prescriptions.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    PharmacyDbContext db,
    IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(db.Prescriptions,
        prescription => prescription.Id == PrescriptionId.From(id), prescription => prescription.FacilityId,
        httpContext.User, HisHopePermissions.Pharmacy.View, "prescription", id.ToString("D"), ct);
    if (!decision.Allowed) return Results.NotFound();

    var prescription = await cache.GetOrSetAsync<PrescriptionDto>(
        $"prescription:{id}",
        async () => (await mediator.Send(new GetPrescriptionByIdQuery(id), ct))!,
        TimeSpan.FromMinutes(5), ct);
    return prescription is null ? Results.NotFound() : Results.Ok(prescription);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

prescriptions.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"prescriptions:patient:{scopeKey}:{patientId}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new GetPrescriptionsByPatientQuery(patientId,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

prescriptions.MapPost("/", async (
    CreatePrescriptionRequest request,
    IMediator mediator,
    ICacheService cache,
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

    await cache.RemoveByPrefixAsync("prescriptions:", ct);

    return Results.Created($"/api/v1/prescriptions/{prescription.Id}", prescription);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyCreate).WithOpenApi();

prescriptions.MapPut("/{id:guid}/fill", async (
    Guid id, IMediator mediator, ICacheService cache, PharmacyDbContext db,
    IResourceAuthorizationEvaluator authorization, HttpContext httpContext, CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(db.Prescriptions,
        prescription => prescription.Id == PrescriptionId.From(id), prescription => prescription.FacilityId,
        httpContext.User, HisHopePermissions.Pharmacy.Dispense, "prescription", id.ToString("D"), ct);
    if (!decision.Allowed) return Results.NotFound();

    await mediator.Send(new FillPrescriptionCommand(id), ct);
    await cache.RemoveAsync($"prescription:{id}", ct);
    await cache.RemoveByPrefixAsync("prescriptions:", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyDispense).WithOpenApi();

prescriptions.MapPut("/{id:guid}/cancel", async (
    Guid id, CancelPrescriptionRequest request,
    IMediator mediator, ICacheService cache, PharmacyDbContext db,
    IResourceAuthorizationEvaluator authorization, HttpContext httpContext, CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(db.Prescriptions,
        prescription => prescription.Id == PrescriptionId.From(id), prescription => prescription.FacilityId,
        httpContext.User, HisHopePermissions.Pharmacy.Cancel, "prescription", id.ToString("D"), ct);
    if (!decision.Allowed) return Results.NotFound();

    await mediator.Send(new CancelPrescriptionCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"prescription:{id}", ct);
    await cache.RemoveByPrefixAsync("prescriptions:", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyCancel).WithOpenApi();

// Patient-specific prescriptions aggregate endpoint (routed via YARP from /api/v1/patients/{patientId:guid}/prescriptions)
app.MapGet("/api/v1/patients/{patientId:guid}/prescriptions", (Guid patientId) =>
{
    return Results.Ok(new { patientId, items = new List<object>() });
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.PharmacyView).WithOpenApi();

// gRPC
app.MapGrpcService<PharmacyGrpcServiceImpl>();
app.MapGrpcHealthChecksService();

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



