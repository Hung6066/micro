using His.Hope.AspNetCore;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.HealthChecks;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using His.Hope.Authorization.Requirements;
using His.Hope.SharedKernel.Authorization;
using His.Hope.FhirGateway.Application;
using His.Hope.FhirGateway.Api;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using Grpc.Net.ClientFactory;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "FhirGateway");

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddFhirGatewayApplication();

// FHIR is an interoperability facade. Resolve source records through the
// owning services so their resource-level permission and facility filters are
// evaluated with the caller's token instead of manufacturing data locally.
builder.Services.AddGrpcClient<PatientGrpcService.PatientGrpcServiceClient>(options =>
{
    options.Address = new Uri(
        builder.Configuration["Services:PatientGrpc"]
        ?? builder.Configuration["GrpcServices:PatientService"]
        ?? "http://patientservice:5006");
});
builder.Services.AddGrpcClient<ClinicalGrpcService.ClinicalGrpcServiceClient>(options =>
{
    options.Address = new Uri(
        builder.Configuration["Services:ClinicalGrpc"]
        ?? builder.Configuration["GrpcServices:ClinicalService"]
        ?? "http://clinicalservice:5009");
});
builder.Services.AddScoped<IFhirBackendClient, GrpcFhirBackendClient>();

// SECURITY: Use the shared OIDC/JWT configuration so encrypted access tokens
// can be decrypted at the resource boundary and DPoP bindings are enforced
// consistently with the other APIs.
var jwtAuthority = builder.Configuration["Jwt:Authority"];
var jwtMetadataAddress = builder.Configuration["Jwt:MetadataAddress"];
if (!string.IsNullOrWhiteSpace(jwtAuthority) &&
    (string.IsNullOrWhiteSpace(jwtMetadataAddress) ||
     string.Equals(jwtMetadataAddress.TrimEnd('/'), jwtAuthority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
{
    builder.Configuration["Jwt:MetadataAddress"] =
        $"{jwtAuthority.TrimEnd('/')}/.well-known/internal-openid-configuration";
}
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();

// FHIR is an interoperability boundary. A broad human permission alone must
// not authorize a client-credentials token (or a token minted for another
// resource) to read PHI. Require an explicit resource scope in addition to
// the existing permission catalog.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Fhir.Patient.Read", policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(
            new PermissionRequirement(HisHopePermissions.Patients.View),
            new ScopeRequirement("fhir.patient.read"),
            new PrincipalTypeRequirement(AuthorizationConstants.PrincipalTypes.Human)))
    .AddPolicy("Fhir.Encounter.Read", policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(
            new PermissionRequirement(HisHopePermissions.Clinical.View),
            new ScopeRequirement("fhir.encounter.read"),
            new PrincipalTypeRequirement(AuthorizationConstants.PrincipalTypes.Human)));

// Enterprise Infrastructure
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "fhir-gateway");

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Comprehensive Health Checks
builder.Services.AddHealthChecks()
    .AddRedisCheck(
        builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379")!,
        name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        configuration: builder.Configuration);

// Kestrel Configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5040, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

var app = builder.Build();

// Middleware Pipeline (order matters)
app.UseHisHopeServiceDefaults();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

// SECURITY: Authentication & Authorization middleware
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapHisHopeHealthEndpoints();
app.Run();

// Exposed for the ASP.NET Core integration host used by the service contract tests.
public partial class Program;
