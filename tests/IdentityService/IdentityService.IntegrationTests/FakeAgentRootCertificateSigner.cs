using His.Hope.IdentityService.Application.InfraAccess;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>Records sign requests and returns a deterministic certificate (or fails on demand).</summary>
public sealed class FakeAgentRootCertificateSigner : IAgentRootCertificateSigner
{
    public const string PublicKey =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8WwhNchbO4m8i4ipK openship-agent-root@test";

    private readonly object _gate = new();
    private readonly List<SignCall> _calls = [];

    public sealed record SignCall(string PublicKey, string Principal, TimeSpan Ttl, string KeyId);

    /// <summary>When set, the next sign attempts throw this message.</summary>
    public string? FailWith { get; set; }

    public IReadOnlyList<SignCall> Calls
    {
        get { lock (_gate) return _calls.ToArray(); }
    }

    public Task<AgentRootSignedCertificate> SignAsync(string publicKey, string principal, TimeSpan ttl, string keyId, CancellationToken ct)
    {
        lock (_gate) _calls.Add(new SignCall(publicKey, principal, ttl, keyId));
        if (FailWith is { } message)
            throw new AgentRootSigningException(message);
        var serial = Guid.NewGuid().ToString("N")[..16];
        return Task.FromResult(new AgentRootSignedCertificate(
            $"ssh-ed25519-cert-v01@openssh.com AAAAfake{serial} {keyId.Split(' ')[1]}", serial));
    }
}
