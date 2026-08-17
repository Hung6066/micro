using His.Hope.Secrets;
using Microsoft.Extensions.Options;

namespace His.Hope.DatabaseContinuityService;

public sealed class VaultContinuityClient(IVaultTransitClient transit, IOptions<DatabaseContinuityOptions> options)
{
    public async Task<VaultContinuityStatus> GetStatusAsync(CancellationToken ct)
    {
        var value = options.Value;
        try
        {
            var status = await transit.GetKeyStatusAsync(value.VaultTransitKeyName, ct);
            return new(status.Configured, status.Reachable, status.Sealed, status.KeyName, status.KeyVersion, status.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Status is an observability endpoint. A temporarily unavailable Vault
            // must be represented as a degraded response so the admin UI can render
            // the configuration warning; mutation endpoints still fail closed via
            // IsReady and the returned Reachable=false state.
            return new(
                Configured: !string.IsNullOrWhiteSpace(value.VaultTransitKeyName),
                Reachable: false,
                Sealed: false,
                KeyName: value.VaultTransitKeyName,
                KeyVersion: null,
                Error: ex.Message);
        }
    }
}

public sealed record VaultContinuityStatus(bool Configured, bool Reachable, bool Sealed, string KeyName, int? KeyVersion, string? Error);
