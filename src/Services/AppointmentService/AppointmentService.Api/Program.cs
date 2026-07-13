using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Api.GrpcServices;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.IntegrationEvents.Appointment;
using His.Hope.PatientGrpc;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration, "appointment-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!);

builder.Services.AddResiliencePolicies();
builder.Services.AddGrpc(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());

var resilienceConfig = new ResilienceConfiguration();
builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration.GetValue("GrpcServices:PatientService", "https://patientservice:5006")!);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ClientCertificates.Add(LoadClientCertificate(builder.Configuration));
    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
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

builder.Services.AddHealthChecks()
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
    options.ListenAnyIP(5007, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        l.UseHttps(h =>
        {
            h.ServerCertificate = LoadServerCertificate(builder.Configuration);
            h.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            h.AllowAnyClientCertificate = !builder.Environment.IsProduction();
            h.CheckCertificateRevocation = false;
        });
    });
});

var app = builder.Build();

app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

var appointments = new List<Appointment>();
var grp = app.MapGroup("/api/v1/appointments");

grp.MapPost("/", async (
    ScheduleAppointmentRequest request,
    PatientGrpcService.PatientGrpcServiceClient patientClient,
    IEventBus eventBus,
    ICacheService cache,
    ResilienceConfiguration resilience,
    CancellationToken ct) =>
{
    var existsResponse = await patientClient.CheckPatientExistsAsync(
        new PatientExistsRequest { Id = request.PatientId.ToString() }, cancellationToken: ct);
    if (!existsResponse.Exists)
        return Results.Problem("Patient not found", statusCode: 404);

    var type = AppointmentType.FromCode(request.TypeCode);
    var appointment = Appointment.Schedule(request.PatientId, request.ProviderId,
        request.ScheduledDate, request.StartTime, request.DurationMinutes, type,
        request.Reason, request.Location);
    appointments.Add(appointment);

    await eventBus.PublishAsync(new AppointmentScheduledIntegrationEvent(
        Guid.Parse(appointment.Id.ToString()!), appointment.PatientId,
        appointment.ProviderId, appointment.ScheduledDate,
        appointment.StartTime, appointment.EndTime), ct);

    await cache.RemoveByPrefixAsync("appointments:", ct);

    return Results.Created($"/api/v1/appointments/{appointment.Id}", new
    {
        id = appointment.Id.ToString(), patientId = appointment.PatientId,
        providerId = appointment.ProviderId, status = appointment.Status.Code
    });
}).WithOpenApi();

grp.MapPut("/{id:guid}/cancel", (Guid id, CancelRequest request) =>
{
    var apt = appointments.FirstOrDefault(a => a.Id == AppointmentId.From(id));
    return apt is null ? Results.NotFound() : Results.NoContent();
}).WithOpenApi();

grp.MapPut("/{id:guid}/checkin", (Guid id) =>
{
    var apt = appointments.FirstOrDefault(a => a.Id == AppointmentId.From(id));
    if (apt is null) return Results.NotFound();
    apt.CheckIn();
    return Results.NoContent();
}).WithOpenApi();

grp.MapPut("/{id:guid}/checkout", (Guid id) =>
{
    var apt = appointments.FirstOrDefault(a => a.Id == AppointmentId.From(id));
    if (apt is null) return Results.NotFound();
    apt.CheckOut();
    return Results.NoContent();
}).WithOpenApi();

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
