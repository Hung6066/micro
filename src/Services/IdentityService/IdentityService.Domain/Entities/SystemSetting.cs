namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Represents a key-value system setting for the His.Hope EMR platform.
/// Settings are used to configure hospital-wide behavior such as
/// display language, session timeouts, MFA requirements, and billing defaults.
/// </summary>
public class SystemSetting
{
    /// <summary>
    /// Setting key in dot-notation (e.g., "hospital.name", "system.sessionTimeout").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Setting value stored as a string. Consumers parse to the appropriate type.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what this setting controls.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping settings in the UI (e.g., "hospital", "system", "clinical", "billing").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// When the setting was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the person who last updated this setting.
    /// </summary>
    public string? UpdatedBy { get; set; }
}
