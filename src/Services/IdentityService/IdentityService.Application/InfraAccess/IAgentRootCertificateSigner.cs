namespace His.Hope.IdentityService.Application.InfraAccess;

/// <summary>A signed OpenSSH user certificate and the serial number Vault assigned to it.</summary>
public sealed record AgentRootSignedCertificate(string SignedKey, string SerialNumber);

/// <summary>Signs the agent's SSH public key with the Vault SSH CA.</summary>
public interface IAgentRootCertificateSigner
{
    /// <exception cref="AgentRootSigningException">Vault refused or could not be reached.</exception>
    Task<AgentRootSignedCertificate> SignAsync(
        string publicKey, string principal, TimeSpan ttl, string keyId, CancellationToken ct);
}

/// <summary>Signing failed. <see cref="Exception.Message"/> is safe to store and show: it never contains a token or key.</summary>
public sealed class AgentRootSigningException(string message, Exception? inner = null) : Exception(message, inner);
