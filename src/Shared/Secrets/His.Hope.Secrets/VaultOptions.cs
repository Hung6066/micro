namespace His.Hope.Secrets;

public sealed class VaultOptions
{
    public const string SectionName = "Vault";
    public string Address { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool RequireVault { get; set; }
    public string TransitMount { get; set; } = "transit";
    public string AuthMount { get; set; } = "kubernetes";
    public string AuthMethod { get; set; } = "kubernetes";
    public string Role { get; set; } = string.Empty;
    public string JwtTokenFile { get; set; } = "/var/run/secrets/tokens/vault";
    public string SpiffeJwtTokenFile { get; set; } = string.Empty;
    public string SpiffeAudience { get; set; } = "vault";
    public string RoleId { get; set; } = string.Empty;
    public string RoleIdFile { get; set; } = string.Empty;
    public string SecretId { get; set; } = string.Empty;
    public string SecretIdFile { get; set; } = string.Empty;
    public bool AllowStaticToken { get; set; }
    public string TlsCaFile { get; set; } = string.Empty;
    public string SecretsMount { get; set; } = "secret";
    public string SecretsPathPrefix { get; set; } = "his-hope";
}
