using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingComplianceStore
{
    IReadOnlyList<SopArtifactDto> GetSopArtifacts(string tenantKey, string? artifactKey, string? status, int limit);
    (SopArtifactDto? Artifact, string? Error) CreateSopArtifact(CreateSopArtifactRequest request, string tenantKey, string actor);
    (SopArtifactDto? Artifact, string? Error) ChangeSopArtifactStatus(Guid artifactId, string tenantKey, string targetStatus, SopArtifactLifecycleRequest request);
    (SopArtifactAcknowledgmentDto? Acknowledgment, string? Error) AcknowledgeSopArtifact(Guid artifactId, string tenantKey, string actor, SopArtifactAcknowledgmentRequest request);
    (BusinessSignatureDto? Signature, string? Error) CreateBusinessSignature(string tenantKey, string actor, CreateBusinessSignatureRequest request);
    IReadOnlyList<BusinessSignatureDto> GetBusinessSignatures(string tenantKey, string? entityType, Guid? entityId, int limit);
}
