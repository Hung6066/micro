using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.FhirGateway.Api;
using His.Hope.ClinicalGrpc;
using His.Hope.PatientGrpc;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace His.Hope.FhirGateway.Contract.Tests;

public sealed class FhirHttpAuthorizationTests : IClassFixture<FhirHttpFactory>
{
    private readonly HttpClient _client;

    public FhirHttpAuthorizationTests(FhirHttpFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Direct_patient_request_without_authentication_is_rejected()
    {
        var response = await _client.GetAsync("/fhir/r4/Patient/123");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Direct_patient_request_without_resource_scope_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/r4/Patient/123");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test", "permission-only");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Direct_patient_request_with_permission_and_scope_is_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/r4/Patient/123");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test", "patient-read");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/fhir+json");
    }

    [Fact]
    public async Task Direct_patient_request_from_workload_principal_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/r4/Patient/123");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test", "patient-read-workload");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public sealed class FhirHttpFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("REDIS_URL", "localhost:6379");
        builder.UseSetting("SERVICE_PATIENT_GRPC_URL", "http://localhost:5006");
        builder.UseSetting("SERVICE_CLINICAL_GRPC_URL", "http://localhost:5007");
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IFhirBackendClient, FakeFhirBackendClient>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestHttpAuthHandler.Scheme;
                options.DefaultChallengeScheme = TestHttpAuthHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestHttpAuthHandler>(
                TestHttpAuthHandler.Scheme, _ => { });
        });
    }
}

public sealed class FakeFhirBackendClient : IFhirBackendClient
{
    public Task<PatientResponse> GetPatientAsync(string id, Metadata headers, CancellationToken cancellationToken) =>
        Task.FromResult(new PatientResponse
        {
            Id = id,
            FirstName = "Test",
            LastName = "Patient",
            GenderCode = "F",
            DateOfBirth = Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(1985, 3, 15), DateTimeKind.Utc)),
            IsActive = true
        });

    public Task<PatientListResponse> SearchPatientsAsync(PatientSearchRequest request, Metadata headers, CancellationToken cancellationToken) =>
        Task.FromResult(new PatientListResponse { TotalCount = 0, Page = request.Page, PageSize = request.PageSize });

    public Task<EncounterResponse> GetEncounterAsync(string id, Metadata headers, CancellationToken cancellationToken) =>
        Task.FromResult(new EncounterResponse
        {
            Id = id,
            PatientId = "550e8400-e29b-41d4-a716-446655440000",
            StatusCode = "IN_PROGRESS",
            EncounterTypeCode = "AMB",
            EncounterTypeName = "ambulatory",
            EncounterDate = Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(2026, 7, 16, 1, 30, 0), DateTimeKind.Utc))
        });
}

public sealed class TestHttpAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var mode = Request.Headers.Authorization.ToString();
        if (!mode.StartsWith("Test ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new("sub", "direct-service-test"),
            new("facility_id", "facility-1")
        };

        if (mode.Equals("Test permission-only", StringComparison.OrdinalIgnoreCase))
            claims.Add(new Claim("permissions", "patients.view"));
        else if (mode.Equals("Test patient-read", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permissions", "patients.view"));
            claims.Add(new Claim("scope", "fhir.patient.read"));
            claims.Add(new Claim("principal_type", "human"));
        }
        else if (mode.Equals("Test patient-read-workload", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permissions", "patients.view"));
            claims.Add(new Claim("scope", "fhir.patient.read"));
            claims.Add(new Claim("principal_type", "workload"));
        }
        else
            return Task.FromResult(AuthenticateResult.Fail("unknown test token"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
