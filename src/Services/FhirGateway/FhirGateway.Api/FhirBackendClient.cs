using Grpc.Core;
using His.Hope.ClinicalGrpc;
using His.Hope.PatientGrpc;

namespace His.Hope.FhirGateway.Api;

/// <summary>
/// Downstream source contract for the FHIR facade. Keeping this boundary
/// injectable makes the HTTP contract testable without bypassing production
/// authorization or returning fabricated resources.
/// </summary>
public interface IFhirBackendClient
{
    Task<PatientResponse> GetPatientAsync(string id, Metadata headers, CancellationToken cancellationToken);
    Task<PatientListResponse> SearchPatientsAsync(PatientSearchRequest request, Metadata headers, CancellationToken cancellationToken);
    Task<EncounterResponse> GetEncounterAsync(string id, Metadata headers, CancellationToken cancellationToken);
}

public sealed class GrpcFhirBackendClient(
    PatientGrpcService.PatientGrpcServiceClient patients,
    ClinicalGrpcService.ClinicalGrpcServiceClient clinical) : IFhirBackendClient
{
    public Task<PatientResponse> GetPatientAsync(string id, Metadata headers, CancellationToken cancellationToken) =>
        patients.GetPatientAsync(new PatientRequest { Id = id }, headers: headers, cancellationToken: cancellationToken).ResponseAsync;

    public Task<PatientListResponse> SearchPatientsAsync(PatientSearchRequest request, Metadata headers, CancellationToken cancellationToken) =>
        patients.SearchPatientsAsync(request, headers: headers, cancellationToken: cancellationToken).ResponseAsync;

    public Task<EncounterResponse> GetEncounterAsync(string id, Metadata headers, CancellationToken cancellationToken) =>
        clinical.GetEncounterAsync(new EncounterRequest { Id = id }, headers: headers, cancellationToken: cancellationToken).ResponseAsync;
}
