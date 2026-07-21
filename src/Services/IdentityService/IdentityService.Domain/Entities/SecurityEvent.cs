namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Security events log for audit and threat detection (HIPAA 164.312(b)).
/// Records authentication events, permission changes, and suspicious activity.
/// </summary>
public class SecurityEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string EventType { get; set; } = string.Empty;  // login_failed, login_success, lockout, password_changed, mfa_enrolled, mfa_failed, suspicious_ip, token_reuse
    public string? Severity { get; set; } = "info";         // info, warning, critical
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Details { get; set; }                     // JSON with extra context
    public string? GeoCountry { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
