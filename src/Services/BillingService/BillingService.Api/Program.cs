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
using His.Hope.IntegrationEvents.Billing;
using His.Hope.BillingService.Api.GrpcServices;
using His.Hope.BillingService.Api.Middleware;
using His.Hope.BillingService.Application;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Infrastructure;
using His.Hope.BillingService.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBillingApplication();
builder.Services.AddBillingInfrastructure(builder.Configuration);

// SECURITY: Add JWT Bearer authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "billing-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

builder.Services.AddResiliencePolicies();
builder.Services.AddOutbox<BillingDbContext>();

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
    options.ExchangeName = "his_hope_billing";
    options.UseSsl = builder.Configuration.GetValue("EventBus:UseSsl", false);
    options.ClientCertificatePath = builder.Configuration["EventBus:ClientCertificatePath"];
    options.ClientCertificatePassword = builder.Configuration["EventBus:ClientCertificatePassword"];
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BillingDbContext>(name: "billing-db", tags: ["database"])
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
    options.ListenAnyIP(5021, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadServerCertificate(builder.Configuration);
            httpsOptions.CheckCertificateRevocation = false;
        });
    });

    options.ListenAnyIP(5022, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.ListenAnyIP(5026, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.ListenAnyIP(5025, listenOptions =>
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

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
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

// Invoice Endpoints (all require JWT authorization with specific permissions)
var invoices = app.MapGroup("/api/v1/invoices").RequireAuthorization();

invoices.MapGet("/", async (
    int page = 1,
    int pageSize = 20,
    string? search = null,
    Guid? patientId = null,
    string? status = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"invoices:search:{search}:{page}:{pageSize}:{patientId}:{status}:{dateFrom}:{dateTo}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchInvoicesQuery(
            search ?? "", page, pageSize, patientId, status, dateFrom, dateTo), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:billing.view").WithOpenApi();

invoices.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var invoice = await cache.GetOrSetAsync(
        $"invoice:{id}",
        async () => await mediator.Send(new GetInvoiceByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
}).RequireAuthorization("Permission:billing.view").WithOpenApi();

invoices.MapGet("/number/{invoiceNumber}", async (
    string invoiceNumber,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var invoice = await cache.GetOrSetAsync(
        $"invoice:number:{invoiceNumber}",
        async () => await mediator.Send(new GetInvoiceByNumberQuery(invoiceNumber), ct),
        TimeSpan.FromMinutes(5), ct);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
}).RequireAuthorization("Permission:billing.view").WithOpenApi();

invoices.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var cacheKey = $"invoices:patient:{patientId}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new GetInvoicesByPatientQuery(patientId), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization("Permission:billing.view").WithOpenApi();

invoices.MapPost("/", async (
    CreateInvoiceRequest request,
    IMediator mediator,
    ICacheService cache,
    IEventBus eventBus,
    CancellationToken ct) =>
{
    var command = new CreateInvoiceCommand(
        request.PatientId, request.EncounterId, request.InvoiceDate,
        request.DueDate, request.InvoiceNumber, request.Notes,
        request.LineItems);

    var invoice = await mediator.Send(command, ct);

    await eventBus.PublishAsync(new InvoiceCreatedIntegrationEvent(
        invoice.Id, invoice.PatientId, invoice.InvoiceNumber,
        invoice.TotalAmount), ct);

    await cache.RemoveByPrefixAsync("invoices:", ct);

    return Results.Created($"/api/v1/invoices/{invoice.Id}", invoice);
}).RequireAuthorization("Permission:billing.create").WithOpenApi();

invoices.MapPost("/{id:guid}/payments", async (
    Guid id, RecordPaymentRequest request,
    IMediator mediator, ICacheService cache,
    IEventBus eventBus, CancellationToken ct) =>
{
    var invoice = await mediator.Send(new RecordPaymentCommand(
        id, request.PatientId, request.Amount, request.PaymentDate,
        request.MethodCode, request.ReferenceNumber, request.Notes), ct);

    await cache.RemoveAsync($"invoice:{id}", ct);
    await cache.RemoveByPrefixAsync("invoices:", ct);

    if (invoice.StatusCode == "PAID")
    {
        await eventBus.PublishAsync(new InvoicePaidIntegrationEvent(
            invoice.Id, invoice.PatientId, request.Amount,
            invoice.TotalAmount), ct);
    }

    return Results.Ok(invoice);
}).RequireAuthorization("Permission:billing.pay").WithOpenApi();

invoices.MapPut("/{id:guid}/void", async (
    Guid id, VoidInvoiceRequest request,
    IMediator mediator, ICacheService cache, CancellationToken ct) =>
{
    await mediator.Send(new VoidInvoiceCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"invoice:{id}", ct);
    await cache.RemoveByPrefixAsync("invoices:", ct);
    return Results.NoContent();
}).RequireAuthorization("Permission:billing.void").WithOpenApi();

// gRPC
app.MapGrpcService<BillingGrpcServiceImpl>();
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
        "CN=his-hope-billing, O=His.Hope", rsa,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(false, false, 0, true));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
        System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment, false));
    req.CertificateExtensions.Add(new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
        new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
    var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost"); san.AddDnsName("billingservice");
    req.CertificateExtensions.Add(san.Build());
    var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return cert;
}

// Request Records
public record CreateInvoiceRequest(
    Guid PatientId,
    Guid? EncounterId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string? Notes,
    ICollection<LineItemInput> LineItems);

public record AddInvoiceLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice,
    string? ItemCode,
    string? ItemTypeCode);

public record RecordPaymentRequest(
    Guid PatientId,
    decimal Amount,
    DateTime PaymentDate,
    string MethodCode,
    string? ReferenceNumber,
    string? Notes);

public record CancelInvoiceRequest(string Reason);

public record VoidInvoiceRequest(string Reason);

public record ApplyDiscountRequest(decimal Amount);

public record ApplyTaxRequest(decimal Amount);

