using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingComplianceStore
{
    Task<IReadOnlyList<SopArtifactDto>> GetSopArtifactsAsync(string tenantKey, string? artifactKey, string? status, int limit, CancellationToken cancellationToken);
    Task<(SopArtifactDto? Artifact, string? Error)> CreateSopArtifactAsync(CreateSopArtifactRequest request, string tenantKey, string actor, CancellationToken cancellationToken);
    Task<(SopArtifactDto? Artifact, string? Error)> ChangeSopArtifactStatusAsync(Guid artifactId, string tenantKey, string targetStatus, SopArtifactLifecycleRequest request, CancellationToken cancellationToken);
    Task<(SopArtifactAcknowledgmentDto? Acknowledgment, string? Error)> AcknowledgeSopArtifactAsync(Guid artifactId, string tenantKey, string actor, SopArtifactAcknowledgmentRequest request, CancellationToken cancellationToken);
    Task<(BusinessSignatureDto? Signature, string? Error)> CreateBusinessSignatureAsync(string tenantKey, string actor, CreateBusinessSignatureRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessSignatureDto>> GetBusinessSignaturesAsync(string tenantKey, string? entityType, Guid? entityId, int limit, CancellationToken cancellationToken);
}
