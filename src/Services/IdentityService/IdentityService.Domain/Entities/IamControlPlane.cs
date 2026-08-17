namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Canonical AWS-like control-plane scope: organization, tenant/account and environment.</summary>
public sealed class IamScope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = "tenant";
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Server-owned catalog of services and their canonical permission namespace.</summary>
public sealed class IamServiceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PermissionPrefix { get; set; } = string.Empty;
    public string Owner { get; set; } = "identity-service";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Reusable permission set; policy is evaluated server-side and never trusted from the UI.</summary>
public sealed class IamPermissionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public string PermissionsJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "draft";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}

/// <summary>Assignment of a permission set to a human or workload principal in a scope.</summary>
public sealed class IamPermissionSetAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PermissionSetId { get; set; }
    public Guid PrincipalId { get; set; }
    public string PrincipalType { get; set; } = "human";
    public Guid ScopeId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = "active";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Workload role trust boundary. It is separate from workforce permission sets.</summary>
public sealed class IamWorkloadRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public string Audience { get; set; } = string.Empty;
    public string TrustPolicyJson { get; set; } = "{}";
    public string PermissionsJson { get; set; } = "[]";
    public int MaxSessionSeconds { get; set; } = 900;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Maximum permission envelope a delegated principal is allowed to grant.</summary>
public sealed class IamPermissionBoundary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrincipalId { get; set; }
    public string PrincipalType { get; set; } = "human";
    public Guid ScopeId { get; set; }
    public string AllowedPermissionsJson { get; set; } = "[]";
    public string ResourceConstraintsJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

/// <summary>Tenant-scoped workforce group used as an assignment principal.</summary>
public sealed class IamGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

public sealed class IamGroupMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

/// <summary>Resource-owned policy envelope evaluated in addition to identity grants.</summary>
public sealed class IamResourcePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScopeId { get; set; }
    public string ServiceKey { get; set; } = string.Empty;
    public string ResourcePattern { get; set; } = string.Empty;
    public string StatementsJson { get; set; } = "[]";
    public string LifecycleStatus { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public string? CreatedBy { get; set; }
}
