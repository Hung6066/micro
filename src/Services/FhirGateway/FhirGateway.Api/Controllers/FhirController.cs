using System.Text;
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
    [Authorize]
    public async Task<IActionResult> GetPatientById(string id)
    {
        // STUB: Will call PatientService via gRPC in a future iteration.
        // For now, return a sample Patient resource to demonstrate the adapter.

        var patient = PatientFhirAdapter.ToFhir(
            id: id,
            firstName: "Hương",
            lastName: "Nguyễn",
            middleName: "Thị",
            dateOfBirth: new DateTimeOffset(1985, 3, 15, 0, 0, 0, TimeSpan.Zero),
            genderCode: "F",
            phone: "0987654321",
            email: "huong.nguyen@example.com",
            isActive: true);

        var json = await Serializer.SerializeToStringAsync(patient);
        return Content(json, "application/fhir+json", Encoding.UTF8);
    }

    /// <summary>
    ///     GET /fhir/r4/Patient
    /// Searches for Patient resources using FHIR search parameters.
    /// Supported parameters: name (partial match), identifier, birthdate (exact).
    /// </summary>
    [HttpGet("Patient")]
    [Authorize]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string? name = null,
        [FromQuery] string? identifier = null,
        [FromQuery] string? birthdate = null)
    {
        // STUB: Will call PatientService gRPC SearchPatients in a future iteration.
        // For now, return a sample Bundle with one Patient entry.

        var patient = PatientFhirAdapter.ToFhir(
            id: "550e8400-e29b-41d4-a716-446655440000",
            firstName: "Hương",
            lastName: "Nguyễn",
            middleName: "Thị",
            dateOfBirth: new DateTimeOffset(1985, 3, 15, 0, 0, 0, TimeSpan.Zero),
            genderCode: "F",
            phone: "0987654321",
            email: "huong.nguyen@example.com",
            isActive: true);

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Id = Guid.NewGuid().ToString(),
            Total = 1,
            Entry = new List<Bundle.EntryComponent>
            {
                new()
                {
                    FullUrl = $"{Request.Scheme}://{Request.Host}/fhir/r4/Patient/{patient.Id}",
                    Resource = patient,
                    Search = new Bundle.SearchComponent
                    {
                        Mode = Bundle.SearchEntryMode.Match
                    }
                }
            }
        };

        var json = await Serializer.SerializeToStringAsync(bundle);
        return Content(json, "application/fhir+json", Encoding.UTF8);
    }

    /// <summary>
    ///     GET /fhir/r4/Encounter/{id}
    /// Retrieves an Encounter resource by His.Hope internal identifier.
    /// </summary>
    [HttpGet("Encounter/{id}")]
    [Authorize]
    public async Task<IActionResult> GetEncounterById(string id)
    {
        // STUB: Will call AppointmentService/ClinicalService via gRPC in a future iteration.

        var encounter = EncounterFhirAdapter.ToFhir(
            id: id,
            patientId: "550e8400-e29b-41d4-a716-446655440000",
            statusCode: "IN_PROGRESS",
            classCode: "AMB",
            className: "khám ngoại trú",
            periodStart: new DateTimeOffset(2026, 7, 16, 8, 30, 0, TimeSpan.FromHours(7)),
            periodEnd: null);

        var json = await Serializer.SerializeToStringAsync(encounter);
        return Content(json, "application/fhir+json", Encoding.UTF8);
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
}
