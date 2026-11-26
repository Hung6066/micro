using System.Text.RegularExpressions;

namespace His.Hope.IdentityService.Application.InfraAccess;

/// <summary>
/// Time-limited root SSH for the infrastructure automation agent. A super admin requests a
/// grant, a different super admin approves it with a fresh MFA, and IdentityService asks the
/// Vault SSH CA (<see cref="VaultMount"/>/<see cref="VaultRole"/>) to sign the key of every active
/// device (admin-app "Agent root devices") for <see cref="Principal"/>. Nodes trust that CA for the principal; nothing else is needed
/// on the node side.
/// </summary>
public sealed class AgentRootOptions
{
    public const string SectionName = "InfraAccess:AgentRoot";

    public bool Enabled { get; set; }

    /// <summary>Identifier of the agent; the certificate fetcher asks for this id.</summary>
    public string AgentId { get; set; } = "openship-agent-root";

    public string VaultMount { get; set; } = "ssh-client-signer";

    public string VaultRole { get; set; } = "agent-root";

    public string Principal { get; set; } = "root-breakglass";

    public int MinMinutes { get; set; } = 30;

    public int MaxMinutes { get; set; } = 480;

    /// <summary>A request that nobody approved within this window can no longer be approved.</summary>
    public int PendingTtlMinutes { get; set; } = 60;

    private static readonly Regex Ed25519PublicKey = new(
        @"^ssh-ed25519 AAAAC3NzaC1lZDI1NTE5[A-Za-z0-9+/]{40,}={0,2}( [\x21-\x7E]{1,200})?$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>True for a device name usable in a certificate key ID.</summary>
    public static bool IsValidDeviceName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && Segment.IsMatch(value);

    /// <summary>OpenSSH "SHA256:..." fingerprint of a valid ed25519 public key line.</summary>
    public static string Fingerprint(string publicKey)
    {
        var blob = Convert.FromBase64String(publicKey.Trim().Split(' ')[1]);
        return "SHA256:" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(blob)).TrimEnd('=');
    }

    private static readonly Regex Segment = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>True for a single-line ed25519 OpenSSH public key whose base64 body decodes.</summary>
    public static bool IsValidPublicKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || !Ed25519PublicKey.IsMatch(value))
            return false;
        var body = value.Split(' ')[1];
        var buffer = new byte[body.Length];
        // An ed25519 key blob is 51 bytes: length-prefixed "ssh-ed25519" + 32-byte key.
        return Convert.TryFromBase64String(body, buffer, out var written) && written == 51;
    }

    /// <summary>Configuration errors, empty when the options are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!Segment.IsMatch(AgentId ?? string.Empty)) errors.Add($"{SectionName}:AgentId is invalid.");
        if (!Segment.IsMatch(VaultMount ?? string.Empty)) errors.Add($"{SectionName}:VaultMount is invalid.");
        if (!Segment.IsMatch(VaultRole ?? string.Empty)) errors.Add($"{SectionName}:VaultRole is invalid.");
        if (!Segment.IsMatch(Principal ?? string.Empty)) errors.Add($"{SectionName}:Principal is invalid.");
        if (MinMinutes < 1 || MaxMinutes < MinMinutes || MaxMinutes > 24 * 60)
            errors.Add($"{SectionName}:MinMinutes/MaxMinutes must satisfy 1 <= Min <= Max <= 1440.");
        if (PendingTtlMinutes is < 1 or > 24 * 60)
            errors.Add($"{SectionName}:PendingTtlMinutes must be between 1 and 1440.");
        return errors;
    }
}
