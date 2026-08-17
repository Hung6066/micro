using His.Hope.SharedKernel.Authorization;

namespace His.Hope.IdentityService.Application.DTOs;

// ============================================================================
// User DTOs
// ============================================================================

public record UserDetailDto(
    Guid Id,
    string UserName,
    string Email,
    string? PhoneNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string? LicenseNumber,
    string? Specialty,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IList<string> Roles,
    string? ConcurrencyToken = null);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? LicenseNumber,
    string? Specialty,
    string? PhoneNumber,
    string? Role);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string? Role,
    bool? IsActive,
    string? ConcurrencyToken = null);

public record AssignRolesRequest(
    string[] RoleIds);

// ============================================================================
// Role & Permission DTOs
// ============================================================================

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    DateTime CreatedAt,
    List<PermissionDto>? Permissions,
    string? ConcurrencyToken = null,
    string Owner = "identity-service",
    int Version = 1,
    string RiskTier = "standard",
    int ReviewCadenceDays = 180,
    string LifecycleStatus = "active",
    DateTime? PublishedAt = null,
    string? PublishedBy = null);

public record CreateRoleRequest(
    string Name,
    string? Description,
    string[]? Permissions,
    string? Owner = null);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    string[]? Permissions,
    string? ConcurrencyToken = null,
    string? Owner = null);

public record PermissionDto(
    string Code,
    string Name,
    string Group,
    string? Description,
    bool IsSystem)
{
    private PermissionDescriptor? Descriptor => HisHopePermissions.FindDescriptor(Code);

    public string Owner => Descriptor?.Owner ?? "unknown";
    public int Version => Descriptor?.Version ?? 1;
    public string RiskTier => Descriptor?.RiskTier ?? "standard";
    public string RequiredAssurance => Descriptor?.RequiredAssurance ?? "standard";
    public string AuditClass => Descriptor?.AuditClass ?? "authorization";
    public bool IsDeprecated => Descriptor?.IsDeprecated ?? false;
    public string? ReplacedBy => Descriptor?.ReplacedBy;
}

// ============================================================================
// System Settings DTOs
// ============================================================================

public record SystemSettingDto(
    string Key,
    string Value,
    string? Description,
    string? Category,
    DateTime UpdatedAt,
    string? UpdatedBy);

public record UpdateSettingRequest(
    string Value,
    string? Description);

public record BulkUpdateSettingItem(
    string Key,
    string Value);

// ============================================================================
// Audit Log DTOs
// ============================================================================

public record AuditLogDto(
    Guid Id,
    string UserId,
    string? UserName,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? Details,
    string? IpAddress,
    string? UserAgent,
    DateTime Timestamp);
