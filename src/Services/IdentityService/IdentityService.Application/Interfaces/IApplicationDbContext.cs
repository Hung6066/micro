using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.Interfaces;

/// <summary>
/// Abstraction of the Identity DbContext for use in the Application layer.
/// This avoids a direct dependency on the Infrastructure layer (Clean Architecture).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<UserMfa> UserMfas { get; }
    DbSet<Domain.Entities.User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<IdentityUserClaim<Guid>> UserClaims { get; }
    DbSet<TableView> TableViews { get; }
    DbSet<MobileDeviceRegistration> MobileDeviceRegistrations { get; }
    DbSet<MobileTelemetryEvent> MobileTelemetryEvents { get; }
    DbSet<PushNotificationOutbox> PushNotificationOutbox { get; }
    DbSet<InAppNotification> InAppNotifications { get; }
    DbSet<PushDeliveryAttempt> PushDeliveryAttempts { get; }
    DbSet<UserFacility> UserFacilities { get; }
    DbSet<BreakGlassRequest> BreakGlassRequests { get; }
    DbSet<AccessRequest> AccessRequests { get; }
    DbSet<AccessReview> AccessReviews { get; }
    DbSet<RoleTemplateVersion> RoleTemplateVersions { get; }
    DbSet<AuthorizationPolicyDefinition> AuthorizationPolicies { get; }
    DbSet<AuthorizationPolicyBundleArtifact> AuthorizationPolicyBundles { get; }
    DbSet<AuthorizationChangeRequest> AuthorizationChangeRequests { get; }
    DbSet<IamScope> IamScopes { get; }
    DbSet<IamServiceDefinition> IamServiceDefinitions { get; }
    DbSet<IamPermissionSet> IamPermissionSets { get; }
    DbSet<IamPermissionSetAssignment> IamPermissionSetAssignments { get; }
    DbSet<IamWorkloadRole> IamWorkloadRoles { get; }
    DbSet<IamPermissionBoundary> IamPermissionBoundaries { get; }
    DbSet<IamGroupMembership> IamGroupMemberships { get; }
    DbSet<IamResourcePolicy> IamResourcePolicies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
