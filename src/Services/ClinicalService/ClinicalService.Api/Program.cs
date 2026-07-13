using System.Security.Cryptography.X509Certificates;
using His.Hope.AppointmentGrpc;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.IntegrationEvents.Clinical;
using His.Hope.PatientGrpc;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration, "clinical-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!);

builder.Services.AddResiliencePolicies();

var resilienceConfig = new ResilienceConfiguration();
var pipeline = resilienceConfig.BuildGenericPipeline("grpc");

builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration.GetValue("GrpcServices:PatientService", "https://patientservice:5006")!);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ClientCertificates = { LoadClientCertificate(builder.Configuration) },
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
})
.AddPolicyHandler(pipeline.AsPolicy());

builder.Services.AddGrpcClient<AppointmentGrpcService.AppointmentGrpcServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration.GetValue("GrpcServices:AppointmentService", "https://appointmentservice:5007")!);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ClientCertificates = { LoadClientCertificate(builder.Configuration) },
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
})
.AddPolicyHandler(pipeline.AsPolicy());

builder.Services.AddRabbitMQEventBus(options =>
{
    options.HostName = builder.Configuration.GetValue("EventBus:HostName", "localhost")!;
    options.Port = builder.Configuration.GetValue("EventBus:Port", 5672);
    options.UserName = builder.Configuration.GetValue("EventBus:UserName", "admin")!;
    options.Password = builder.Configuration.GetValue("EventBus:Password", "admin")!;
    options.ExchangeName = "his_hope_clinical";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
});

builder.Services.AddHealthChecks()
    .AddRabbitMQCheck(builder.Configuration.GetValue("EventBus:HostName", "localhost")!,
        builder.Configuration.GetValue("EventBus:Port", 5672),
        builder.Configuration.GetValue("EventBus:UserName", "admin")!,
        builder.Configuration.GetValue("EventBus:Password", "admin")!, name: "rabbitmq",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddRedisCheck(builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddGrpcServiceCheck("patient-service",
        builder.Configuration.GetValue("GrpcServices:PatientService", "https://patientservice:5006")!,
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddGrpcServiceCheck("appointment-service",
        builder.Configuration.GetValue("GrpcServices:AppointmentService", "https://appointmentservice:5007")!,
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5004, l =>
    {
        l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        l.UseHttps(LoadServerCertificate(builder.Configuration));
    });
});

var app = builder.Build();

app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

var encounters = new List<Encounter>();
var grp = app.MapGroup("/api/v1/encounters");

grp.MapPost("/", async (
    StartEncounterRequest request,
    PatientGrpcService.PatientGrpcServiceClient patientClient,
    AppointmentGrpcService.AppointmentGrpcServiceClient? appointmentClient,
    IEventBus eventBus,
    ICacheService cache,
    CancellationToken ct) =>
{
    var patientResponse = await patientClient.GetPatientAsync(
        new PatientRequest { Id = request.PatientId.ToString() }, cancellationToken: ct);

    if (request.AppointmentId.HasValue)
    {
        var aptResponse = await appointmentClient!.CheckAppointmentExistsAsync(
            new AppointmentExistsRequest { Id = request.AppointmentId.Value.ToString() }, cancellationToken: ct);
        if (!aptResponse.Exists) return Results.Problem("Appointment not found", statusCode: 404);
    }

    var type = EncounterType.FromCode(request.EncounterTypeCode);
    var encounter = Encounter.Start(request.PatientId, request.ProviderId, type);
    encounters.Add(encounter);

    await eventBus.PublishAsync(new EncounterStartedIntegrationEvent(
        Guid.Parse(encounter.Id.ToString()!), encounter.PatientId,
        encounter.ProviderId, request.AppointmentId,
        encounter.EncounterType.Code, encounter.EncounterDate), ct);

    await cache.RemoveByPrefixAsync("encounters:", ct);

    return Results.Created($"/api/v1/encounters/{encounter.Id}", new
    {
        id = encounter.Id.ToString(),
        patientName = patientResponse.FullName,
        patientId = encounter.PatientId, providerId = encounter.ProviderId,
        encounterType = encounter.EncounterType.Code,
        status = encounter.Status.Code, encounterDate = encounter.EncounterDate
    });
}).WithOpenApi();

grp.MapPost("/{id:guid}/vitals", (Guid id, RecordVitalsRequest request) =>
{
    var e = encounters.FirstOrDefault(x => x.Id == EncounterId.From(id));
    if (e is null) return Results.NotFound();
    e.RecordVitals(request.Temperature, request.HeartRate, request.RespiratoryRate,
        request.SystolicBP, request.DiastolicBP, request.OxygenSaturation,
        request.HeightCm, request.WeightKg, request.Bmi);
    return Results.NoContent();
}).WithOpenApi();

grp.MapPost("/{id:guid}/diagnosis", (Guid id, AddDiagnosisRequest request) =>
{
    var e = encounters.FirstOrDefault(x => x.Id == EncounterId.From(id));
    if (e is null) return Results.NotFound();
    e.AddDiagnosis(new Diagnosis(request.ConditionName, request.Icd10Code, request.IsPrimary, request.Notes));
    return Results.NoContent();
}).WithOpenApi();

grp.MapPut("/{id:guid}/complete", (Guid id) =>
{
    var e = encounters.FirstOrDefault(x => x.Id == EncounterId.From(id));
    if (e is null) return Results.NotFound();
    e.Complete();
    return Results.NoContent();
}).WithOpenApi();

app.MapGrpcHealthChecksService();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, new
        {
            status = report.Status.ToString(), duration = report.TotalDuration.TotalMilliseconds,
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
        : CreateDevCert("clinicalservice");
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
    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Certificates", "server.pfx"),
        cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "his-hope-dev"));
    return cert;
}

public record StartEncounterRequest(Guid PatientId, Guid ProviderId, Guid? AppointmentId, string EncounterTypeCode);
public record RecordVitalsRequest(decimal? Temperature, int? HeartRate, int? RespiratoryRate,
    int? SystolicBP, int? DiastolicBP, decimal? OxygenSaturation, decimal? HeightCm, decimal? WeightKg, decimal? Bmi);
public record AddDiagnosisRequest(string ConditionName, string Icd10Code, bool IsPrimary, string? Notes);
