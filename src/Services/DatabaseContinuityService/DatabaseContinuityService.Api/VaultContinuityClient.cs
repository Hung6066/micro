using His.Hope.Secrets;
using Microsoft.Extensions.Options;

namespace His.Hope.DatabaseContinuityService;

public sealed class VaultContinuityClient(IVaultTransitClient transit, IOptions<DatabaseContinuityOptions> options)
{
    public async Task<VaultContinuityStatus> GetStatusAsync(CancellationToken ct)
    {
        var value = options.Value;
        var status = await transit.GetKeyStatusAsync(value.VaultTransitKeyName, ct);
        return new(status.Configured, status.Reachable, status.Sealed, status.KeyName, status.KeyVersion, status.Error);
    }
}

public sealed record VaultContinuityStatus(bool Configured, bool Reachable, bool Sealed, string KeyName, int? KeyVersion, string? Error);
