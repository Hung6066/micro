using His.Hope.AspNetCore;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using System.Security.Cryptography.X509Certificates;
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
using His.Hope.IntegrationEvents.Billing;
using His.Hope.BillingService.Api.GrpcServices;
using His.Hope.BillingService.Api.Middleware;
using His.Hope.BillingService.Application;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Infrastructure;
using His.Hope.BillingService.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
                .Destructure.With<His.Hope.Infrastructure.Logging.PhiDestructuringPolicy>()
                .Enrich.WithProperty("service", "billing-service"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBillingApplication();
builder.Services.AddBillingInfrastructure(builder.Configuration);
builder.Services.AddHisHopeMigrationRunner<BillingDbContext>();

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// Enterprise Infrastructure
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "billing-service");

builder.Services.AddOutbox<BillingDbContext>();

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});

builder.Services.AddHisHopeLegacyRabbitMqEventBus(builder.Configuration);

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BillingDbContext>(name: "billing-db", tags: ["database"])
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
    options.Listen(System.Net.IPAddress.Any, 5020, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.Listen(System.Net.IPAddress.Any, 5022, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.Listen(System.Net.IPAddress.Any, 5026, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });

    options.Listen(System.Net.IPAddress.Any, 5025, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

if (builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
         builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    using var scope = app.Services.CreateScope();
      await scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateAsync();
}
else if (!app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "BillingService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
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

// Invoice Endpoints (all require JWT authorization with specific permissions)
var invoices = app.MapGroup("/api/v1/invoices").RequireAuthorization();

invoices.MapGet("/", async (
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    int page = 1,
    int pageSize = 20,
    string? search = null,
    Guid? patientId = null,
    string? status = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"invoices:search:{scopeKey}:{search}:{page}:{pageSize}:{patientId}:{status}:{dateFrom}:{dateTo}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchInvoicesQuery(
            search ?? "", page, pageSize, patientId, status, dateFrom, dateTo,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

invoices.MapGet("/search", async (
    string? q,
    int page,
    int pageSize,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct,
    Guid? patientId = null,
    string? status = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"invoices:search:{scopeKey}:{q}:{page}:{pageSize}:{patientId}:{status}:{dateFrom}:{dateTo}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new SearchInvoicesQuery(
            q ?? "", page, pageSize, patientId, status, dateFrom, dateTo,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(2), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

invoices.MapGet("/{id:guid}", async (
    Guid id,
    IMediator mediator,
    ICacheService cache,
    BillingDbContext db,
    IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(db.Invoices,
        invoice => invoice.Id == InvoiceId.From(id), invoice => invoice.FacilityId,
        httpContext.User, HisHopePermissions.Billing.View, "invoice", id.ToString("D"), ct);
    if (!decision.Allowed) return Results.NotFound();

    var invoice = await cache.GetOrSetAsync(
        $"invoice:{id}",
        async () => await mediator.Send(new GetInvoiceByIdQuery(id), ct),
        TimeSpan.FromMinutes(5), ct);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

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
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

invoices.MapGet("/patient/{patientId:guid}", async (
    Guid patientId,
    IMediator mediator,
    ICacheService cache,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var accessScope = FacilityAccessScope.FromPrincipal(httpContext.User);
    var scopeKey = accessScope.IsCrossFacility ? "cross" : string.Join(",", accessScope.FacilityIds.OrderBy(id => id));
    var cacheKey = $"invoices:patient:{scopeKey}:{patientId}";
    var result = await cache.GetOrSetAsync(
        cacheKey,
        async () => await mediator.Send(new GetInvoicesByPatientQuery(patientId,
            accessScope.FacilityIds, accessScope.IsCrossFacility), ct),
        TimeSpan.FromMinutes(5), ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

invoices.MapPost("/", async (
    CreateInvoiceRequest request,
    IMediator mediator,
    ICacheService cache,
    CancellationToken ct) =>
{
    var command = new CreateInvoiceCommand(
        request.PatientId, request.EncounterId, request.InvoiceDate,
        request.DueDate, request.InvoiceNumber, request.Notes,
        request.LineItems
            .Select(item => new LineItemInput(
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.ItemCode,
                item.ItemTypeCode))
            .ToArray());

    var invoice = await mediator.Send(command, ct);

    await cache.RemoveByPrefixAsync("invoices:", ct);

    return Results.Created($"/api/v1/invoices/{invoice.Id}", invoice);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingCreate).WithOpenApi();

invoices.MapPost("/{id:guid}/payments", async (
    Guid id, RecordPaymentRequest request,
    IMediator mediator, ICacheService cache,
    BillingDbContext db, IResourceAuthorizationEvaluator authorization,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(
        db.Invoices,
        invoice => invoice.Id == InvoiceId.From(id),
        invoice => invoice.FacilityId,
        httpContext.User,
        HisHopePermissions.Billing.Pay,
        "invoice",
        id.ToString("D"),
        ct);
    if (!decision.Allowed)
        return Results.NotFound();

    var invoice = await mediator.Send(new RecordPaymentCommand(
        id, request.PatientId, request.Amount, request.PaymentDate,
        request.MethodCode, request.ReferenceNumber, request.Notes), ct);

    await cache.RemoveAsync($"invoice:{id}", ct);
    await cache.RemoveByPrefixAsync("invoices:", ct);

    return Results.Ok(invoice);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingPay).WithOpenApi();

invoices.MapPut("/{id:guid}/void", async (
    Guid id, VoidInvoiceRequest request,
    IMediator mediator, ICacheService cache, BillingDbContext db,
    IResourceAuthorizationEvaluator authorization, HttpContext httpContext,
    CancellationToken ct) =>
{
    var decision = await authorization.EvaluateResourceAsync(
        db.Invoices,
        invoice => invoice.Id == InvoiceId.From(id),
        invoice => invoice.FacilityId,
        httpContext.User,
        HisHopePermissions.Billing.Void,
        "invoice",
        id.ToString("D"),
        ct);
    if (!decision.Allowed)
        return Results.NotFound();

    await mediator.Send(new VoidInvoiceCommand(id, request.Reason), ct);
    await cache.RemoveAsync($"invoice:{id}", ct);
    await cache.RemoveByPrefixAsync("invoices:", ct);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingVoid).WithOpenApi();

// Patient-specific invoices aggregate endpoint (routed via YARP from /api/v1/patients/{patientId:guid}/invoices)
app.MapGet("/api/v1/patients/{patientId:guid}/invoices", async (Guid patientId) =>
{
    return Results.Ok(new { patientId, items = new List<object>() });
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.BillingView).WithOpenApi();

// gRPC
app.MapGrpcService<BillingGrpcServiceImpl>();
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


