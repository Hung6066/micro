using System.Text.Json;

namespace His.Hope.IdentityService.Application.Provisioning;

public sealed record ProvisioningChange(
    string Target,
    string Operation,
    string ResourceType,
    string ResourceId,
    JsonDocument Payload,
    string? ExternalId = null);

public sealed record ProvisioningResult(bool Success, string? ExternalId = null, string? Error = null);

public interface IProvisioningTarget
{
    string Name { get; }
    Task<ProvisioningResult> ApplyAsync(ProvisioningChange change, CancellationToken ct = default);
}
