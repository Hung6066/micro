namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// The Vault-signed OpenSSH certificate of one device for one issued grant. Never returned
/// by list endpoints or written to audit; only the device's own client can fetch it.
/// </summary>
public class AgentRootCertificate
{
    public Guid Id { get; set; }
    public Guid GrantId { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Certificate { get; set; } = string.Empty;
    public string Serial { get; set; } = string.Empty;
    public DateTime ValidBefore { get; set; }
    public DateTime IssuedAt { get; set; }
}
