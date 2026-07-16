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
    IList<string> Roles);

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
    bool? IsActive);

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
    List<PermissionDto>? Permissions);

public record CreateRoleRequest(
    string Name,
    string? Description,
    string[]? Permissions);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    string[]? Permissions);

public record PermissionDto(
    string Code,
    string Name,
    string Group,
    string? Description,
    bool IsSystem);

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
