using System.Text;
using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using His.Hope.FhirGateway.Application.Adapters;

namespace His.Hope.FhirGateway.Api.Controllers;

/// <summary>
/// FHIR R4 API controller exposing Patient and Encounter resources
/// as well as the /metadata CapabilityStatement endpoint.
/// </summary>
[ApiController]
[Route("fhir/r4")]
[Produces("application/fhir+json")]
public class FhirController : ControllerBase
{
    private static readonly FhirJsonSerializer Serializer = new(new SerializerSettings
    {
        Pretty = true,
        AppendNewLine = false
    });

    private readonly IFhirBackendClient _backend;

    public FhirController(IFhirBackendClient backend)
    {
        _backend = backend;
    }

    /// <summary>
    ///     GET /fhir/r4/metadata
    /// Returns the FHIR CapabilityStatement for this server.
    /// </summary>
    [HttpGet("metadata")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMetadata()
    {
        var capability = BuildCapabilityStatement();
        var json = await Serializer.SerializeToStringAsync(capability);
        return Content(json, "application/fhir+json", Encoding.UTF8);
    }

    /// <summary>
    ///     GET /fhir/r4/Patient/{id}
    /// Retrieves a Patient resource by His.Hope internal identifier.
    /// </summary>
    [HttpGet("Patient/{id}")]
    [Authorize(Policy = "Fhir.Patient.Read")]
    public async Task<IActionResult> GetPatientById(string id)
    {
        try
        {
            var response = await _backend.GetPatientAsync(id, ForwardCallerHeaders(), HttpContext.RequestAborted);
            var patient = PatientFhirAdapter.ToFhir(
                response.Id, response.FirstName, response.LastName, response.MiddleName,
                response.DateOfBirth.ToDateTimeOffset(), response.GenderCode,
                response.Phone, response.Email, response.IsActive);
            var json = await Serializer.SerializeToStringAsync(patient);
            return Content(json, "application/fhir+json", Encoding.UTF8);
        }
        catch (RpcException ex)
        {
            return MapGrpcFailure(ex);
        }
    }

    /// <summary>
    ///     GET /fhir/r4/Patient
    /// Searches for Patient resources using FHIR search parameters.
    /// Supported parameters: name (partial match), identifier, birthdate (exact).
    /// </summary>
    [HttpGet("Patient")]
    [Authorize(Policy = "Fhir.Patient.Read")]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string? name = null,
        [FromQuery] string? identifier = null,
        [FromQuery] string? birthdate = null)
    {
        try
        {
            var response = await _backend.SearchPatientsAsync(
                new His.Hope.PatientGrpc.PatientSearchRequest
                {
                    SearchTerm = name ?? identifier ?? string.Empty,
                    Page = 1,
                    PageSize = 100,
                    Filters = { ["birthdate"] = birthdate ?? string.Empty }
                },
                headers: ForwardCallerHeaders(),
                cancellationToken: HttpContext.RequestAborted);

            var entries = response.Patients.Select(item =>
            {
                var resource = PatientFhirAdapter.ToFhir(
                    item.Id, item.FirstName, item.LastName, item.MiddleName,
                    item.DateOfBirth.ToDateTimeOffset(), item.GenderCode,
                    item.Phone, item.Email, item.IsActive);
                return new Bundle.EntryComponent
                {
                    FullUrl = $"{Request.Scheme}://{Request.Host}/fhir/r4/Patient/{resource.Id}",
                    Resource = resource,
                    Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match }
                };
            }).ToList();

            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Searchset,
                Id = Guid.NewGuid().ToString(),
                Total = response.TotalCount,
                Entry = entries
            };
            var json = await Serializer.SerializeToStringAsync(bundle);
            return Content(json, "application/fhir+json", Encoding.UTF8);
        }
        catch (RpcException ex)
        {
            return MapGrpcFailure(ex);
        }
    }

    /// <summary>
    ///     GET /fhir/r4/Encounter/{id}
    /// Retrieves an Encounter resource by His.Hope internal identifier.
    /// </summary>
    [HttpGet("Encounter/{id}")]
    [Authorize(Policy = "Fhir.Encounter.Read")]
    public async Task<IActionResult> GetEncounterById(string id)
    {
        try
        {
            var response = await _backend.GetEncounterAsync(id, ForwardCallerHeaders(), HttpContext.RequestAborted);
            var encounter = EncounterFhirAdapter.ToFhir(
                response.Id, response.PatientId, response.StatusCode,
                response.EncounterTypeCode, response.EncounterTypeName,
                response.EncounterDate.ToDateTimeOffset(), null);
            var json = await Serializer.SerializeToStringAsync(encounter);
            return Content(json, "application/fhir+json", Encoding.UTF8);
        }
        catch (RpcException ex)
        {
            return MapGrpcFailure(ex);
        }
    }

    // -----------------------------------------------------------------------
    //  CapabilityStatement
    // -----------------------------------------------------------------------

    private static CapabilityStatement BuildCapabilityStatement()
    {
        return new CapabilityStatement
        {
            Status = PublicationStatus.Draft,
            Date = "2026-07-18T00:00:00+07:00",
            Publisher = "Bệnh viện Đa khoa X – His.Hope Platform",
            Kind = CapabilityStatementKind.Instance,
            Software = new CapabilityStatement.SoftwareComponent
            {
                Name = "His.Hope FHIR Gateway",
                Version = "1.0.0"
            },
            Implementation = new CapabilityStatement.ImplementationComponent
            {
                Description = "His.Hope FHIR R4 Gateway – external interoperability layer",
                Url = "https://fhir.his.hope.vn/fhir/r4"
            },
            FhirVersion = FHIRVersion.N4_0_1,
            Format = new[] { "application/fhir+json", "application/json" },
            Rest = new List<CapabilityStatement.RestComponent>
            {
                new()
                {
                    Mode = CapabilityStatement.RestfulCapabilityMode.Server,
                    Security = new CapabilityStatement.SecurityComponent
                    {
                        Cors = true,
                        Description = new Markdown(
                            "JWT Bearer token authentication via His.Hope IdentityService. " +
                            "Obtain tokens from https://identity.his.hope.vn/api/v1/auth/login")
                    },
                    Resource = new List<CapabilityStatement.ResourceComponent>
                    {
                        new()
                        {
                            Type = "Patient",
                            Profile = "http://hl7.org/fhir/StructureDefinition/Patient",
                            Interaction = new List<CapabilityStatement.ResourceInteractionComponent>
                            {
                                new() { Code = CapabilityStatement.TypeRestfulInteraction.Read },
                                new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType }
                            },
                            SearchParam = new List<CapabilityStatement.SearchParamComponent>
                            {
                                new()
                                {
                                    Name = "name",
                                    Type = SearchParamType.String,
                                    Documentation = "A patient name (partial match on any part of the name)"
                                },
                                new()
                                {
                                    Name = "identifier",
                                    Type = SearchParamType.Token,
                                    Documentation = "A patient identifier (His.Hope internal ID or external ID)"
                                },
                                new()
                                {
                                    Name = "birthdate",
                                    Type = SearchParamType.Date,
                                    Documentation = "The patient's date of birth (exact match: yyyy-MM-dd)"
                                }
                            }
                        },
                        new()
                        {
                            Type = "Encounter",
                            Profile = "http://hl7.org/fhir/StructureDefinition/Encounter",
                            Interaction = new List<CapabilityStatement.ResourceInteractionComponent>
                            {
                                new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }
                            }
                        }
                    }
                }
            }
        };
    }

    private Metadata ForwardCallerHeaders()
    {
        var headers = new Metadata();
        if (Request.Headers.Authorization.Count > 0)
            headers.Add("authorization", Request.Headers.Authorization.ToString());
        if (Request.Headers.TryGetValue("dpop", out var dpop))
            headers.Add("dpop", dpop.ToString());
        if (Request.Headers.TryGetValue("x-correlation-id", out var correlation))
            headers.Add("x-correlation-id", correlation.ToString());
        return headers;
    }

    private IActionResult MapGrpcFailure(RpcException exception) => exception.StatusCode switch
    {
        Grpc.Core.StatusCode.NotFound or Grpc.Core.StatusCode.PermissionDenied or Grpc.Core.StatusCode.Unauthenticated => NotFound(),
        Grpc.Core.StatusCode.InvalidArgument => BadRequest(new OperationOutcome
        {
            Issue = [new OperationOutcome.IssueComponent
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Invalid,
                Diagnostics = exception.Status.Detail
            }]
        }),
        Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded => base.StatusCode(503),
        _ => base.StatusCode(502)
    };
}
