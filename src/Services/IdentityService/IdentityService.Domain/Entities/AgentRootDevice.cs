namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// A machine allowed to hold the agent's root SSH certificate (the K3s manager, an operator
/// workstation, ...). Each device has its own ed25519 key and its own client_credentials
/// client; an approved <see cref="AgentRootGrant"/> signs every active device key, and a device
/// can fetch only its own certificate.
/// </summary>
public class AgentRootDevice
{
    public Guid Id { get; set; }

    /// <summary>Short unique name, also written into the certificate key ID ("device=...").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One "ssh-ed25519 AAAA... comment" line.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>OpenSSH SHA256 fingerprint of <see cref="PublicKey"/> ("SHA256:...").</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The client_credentials client this device fetches its certificate with.</summary>
    public string ClientId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public Guid AddedBy { get; set; }
    public string AddedByEmail { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public Guid? DisabledBy { get; set; }
    public DateTime? DisabledAt { get; set; }
    public DateTime? LastFetchedAt { get; set; }
}
