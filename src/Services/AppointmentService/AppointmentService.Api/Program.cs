using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Api.GrpcServices;
using His.Hope.AppointmentService.Application;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Infrastructure;
using His.Hope.AppointmentService.Infrastructure.Persistence;
using MediatR;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Security.Authorization;
using His.Hope.Infrastructure.Audit;
using His.Hope.IntegrationEvents.Appointment;
using His.Hope.PatientGrpc;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SECURITY: Add JWT Bearer authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

builder.Services.AddAppointmentApplication();
builder.Services.AddAppointmentInfrastructure(builder.Configuration);

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration, "appointment-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!);

builder.Services.AddResiliencePolicies();
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddSingleton(_ =>
{
    var handler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        UseProxy = false,
        AllowAutoRedirect = false,
    };
    var channel = GrpcChannel.ForAddress("http://localhost:5013", new GrpcChannelOptions
    {
        HttpHandler = handler,
        DisposeHttpClient = true,
        MaxRetryAttempts = 0,
    });
    return new PatientGrpcService.PatientGrpcServiceClient(channel);
});

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_appointment";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
});

builder.Services.AddOutbox<AppointmentDbContext>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppointmentDbContext>(name: "appointment-db", tags: ["database"])
    .AddRabbitMQCheck(
        builder.Configuration.GetValue("EventBus:HostName", "localhost")!,
        builder.Configuration.GetValue("EventBus:Port", 5672),
        builder.Configuration.GetValue("EventBus:UserName", "admin")!,
        builder.Configuration.GetValue("EventBus:Password", "admin")!,
        name: "rabbitmq", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddRedisCheck(
        builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddGrpcServiceCheck("patient-service",
        builder.Configuration.GetValue("GrpcServices:PatientService", "https://patientservice:5006")!,
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5003, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        l.UseHttps(LoadServerCertificate(builder.Configuration));
    });
    options.ListenAnyIP(5009, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    options.ListenAnyIP(5007, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        l.UseHttps(h =>
        {
            h.ServerCertificate = LoadServerCertificate(builder.Configuration);
            h.CheckCertificateRevocation = false;
        });
    });
    options.ListenAnyIP(5014, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

// SECURITY: Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();


app.UsePhiAudit();

// Appointment Endpoints (all require JWT authorization with specific permissions)
var grp = app.MapGroup("/api/v1/appointments").RequireAuthorization();

grp.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var result = await cache.GetOrSetAsync(
        "appointments:all",
        async () => await mediator.Send(new SearchAppointmentsQuery("", 1, 1000), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:appointments.view").WithOpenApi();

grp.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var appointment = await cache.GetOrSetAsync(
        $"appointment:{id}",
        async () => await mediator.Send(new GetAppointmentByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return appointment is null ? Results.NotFound() : Results.Ok(appointment);
}).RequireAuthorization("Permission:appointments.view").WithOpenApi();

grp.MapGet("/search", async (
    string? q, int page, int pageSize,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"appointments:search:{q}:{page}:{pageSize}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchAppointmentsQuery(q ?? "", page, pageSize), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:appointments.view").WithOpenApi();

grp.MapPost("/", async (
    ScheduleAppointmentRequest request,
    PatientGrpcService.PatientGrpcServiceClient patientClient,
    IMediator mediator,
    IEventBus eventBus,
    ICacheService cache,
    CancellationToken ct) =>
{
    var existsResponse = await patientClient.CheckPatientExistsAsync(
        new PatientExistsRequest { Id = request.PatientId.ToString() }, cancellationToken: ct);
    if (!existsResponse.Exists)
        return Results.Problem("Patient not found", statusCode: 404);

    var command = new CreateAppointmentCommand(
        request.PatientId, request.ProviderId, request.ScheduledDate,
        request.StartTime, request.DurationMinutes, request.TypeCode,
        request.Reason, request.Location);

    var appointmentDto = await mediator.Send(command, ct);

    await eventBus.PublishAsync(new AppointmentScheduledIntegrationEvent(
        appointmentDto.Id, appointmentDto.PatientId,
        appointmentDto.ProviderId, appointmentDto.ScheduledDate,
        appointmentDto.StartTime, appointmentDto.EndTime), ct);

    await cache.RemoveByPrefixAsync("appointments:", ct);

    return Results.Created($"/api/v1/appointments/{appointmentDto.Id}", appointmentDto);
}).RequireAuthorization("Permission:appointments.create").WithOpenApi();

grp.MapPut("/{id:guid}/cancel", async (
    Guid id,
    CancelRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CancelAppointmentCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"appointment:{id}", ct);
    await cache.RemoveByPrefixAsync("appointments:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:appointments.cancel").WithOpenApi();

grp.MapPut("/{id:guid}/checkin", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CheckInAppointmentCommand(id), ct);
    await cache.RemoveAsync($"appointment:{id}", ct);
    await cache.RemoveByPrefixAsync("appointments:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:appointments.check-in").WithOpenApi();

grp.MapPut("/{id:guid}/checkout", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    await mediator.Send(new CheckOutAppointmentCommand(id), ct);
    await cache.RemoveAsync($"appointment:{id}", ct);
    await cache.RemoveByPrefixAsync("appointments:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:appointments.update").WithOpenApi();

grp.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    int page,
    int pageSize,
    DateTime? fromDate,
    DateTime? toDate,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"appointments:patient:{patientId}:{page}:{pageSize}:{fromDate}:{toDate}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(
            new GetAppointmentsByPatientQuery(patientId, page, pageSize, fromDate, toDate), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:appointments.view").WithOpenApi();

app.MapGrpcService<AppointmentGrpcServiceImpl>();
app.MapGrpcHealthChecksService();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key, status = e.Value.Status.ToString(),
                description = e.Value.Description, error = e.Value.Exception?.Message,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
    }
}).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.Run();

static X509Certificate2 LoadServerCertificate(IConfiguration c) =>
    !string.IsNullOrEmpty(c["Certificates:Server:Path"])
        ? new X509Certificate2(c["Certificates:Server:Path"]!, c["Certificates:Server:Password"]!)
        : CreateDevCert("appointmentservice");

static X509Certificate2 LoadClientCertificate(IConfiguration c) =>
    !string.IsNullOrEmpty(c["Certificates:Client:Path"])
        ? new X509Certificate2(c["Certificates:Client:Path"]!, c["Certificates:Client:Password"]!)
        : CreateDevCert("his-hope-client");

static X509Certificate2 CreateDevCert(string cn)
{
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
        $"CN={cn}, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1"), new("1.3.6.1.5.5.7.3.2") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName(cn); san.AddDnsName("*.his-hope.internal");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Certificates"));
    var pfx = Path.Combine(AppContext.BaseDirectory, "Certificates", "server.pfx");
    File.WriteAllBytes(pfx, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "his-hope-dev"));
    return cert;
}

public record ScheduleAppointmentRequest(Guid PatientId, Guid ProviderId, DateTime ScheduledDate,
    TimeSpan StartTime, int DurationMinutes, string TypeCode, string? Reason, string? Location);
public record CancelRequest(string? Reason);

