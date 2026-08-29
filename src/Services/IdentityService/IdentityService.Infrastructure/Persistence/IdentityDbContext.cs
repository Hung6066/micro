using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.Infrastructure.DataLifecycle;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

public class IdentityDbContext : IdentityDbContext<User, Role, Guid>, IApplicationDbContext
{
    // Custom entity sets for the extended identity model
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserMfa> UserMfas => Set<UserMfa>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<SecuritySignalOutbox> SecuritySignalOutbox => Set<SecuritySignalOutbox>();
    public DbSet<ClientConsent> ClientConsents => Set<ClientConsent>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<MobileDeviceRegistration> MobileDeviceRegistrations => Set<MobileDeviceRegistration>();
    public DbSet<MobileTelemetryEvent> MobileTelemetryEvents => Set<MobileTelemetryEvent>();
    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();
    public DbSet<PushNotificationOutbox> PushNotificationOutbox => Set<PushNotificationOutbox>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<PushDeliveryAttempt> PushDeliveryAttempts => Set<PushDeliveryAttempt>();
    public DbSet<UserFacility> UserFacilities => Set<UserFacility>();
    public DbSet<BreakGlassRequest> BreakGlassRequests => Set<BreakGlassRequest>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AccessReview> AccessReviews => Set<AccessReview>();
    public DbSet<SupportElevation> SupportElevations => Set<SupportElevation>();
    public DbSet<RoleTemplateVersion> RoleTemplateVersions => Set<RoleTemplateVersion>();
    public DbSet<AuthorizationPolicyDefinition> AuthorizationPolicies => Set<AuthorizationPolicyDefinition>();
    public DbSet<AuthorizationPolicyBundleArtifact> AuthorizationPolicyBundles => Set<AuthorizationPolicyBundleArtifact>();
    public DbSet<AuthorizationChangeRequest> AuthorizationChangeRequests => Set<AuthorizationChangeRequest>();
    public DbSet<UserPasswordHistory> UserPasswordHistories => Set<UserPasswordHistory>();
    public DbSet<UserClientCertificate> UserClientCertificates => Set<UserClientCertificate>();
    public DbSet<DirectoryProvisioningOutbox> DirectoryProvisioningOutbox => Set<DirectoryProvisioningOutbox>();
    public DbSet<DirectoryProvisioningBinding> DirectoryProvisioningBindings => Set<DirectoryProvisioningBinding>();
    public DbSet<LocalizationResource> LocalizationResources => Set<LocalizationResource>();
    public DbSet<LocalizationTranslation> LocalizationTranslations => Set<LocalizationTranslation>();
    public DbSet<DevicePostureAssessment> DevicePostureAssessments => Set<DevicePostureAssessment>();
    public DbSet<DevicePosturePolicy> DevicePosturePolicies => Set<DevicePosturePolicy>();
    public DbSet<IamScope> IamScopes => Set<IamScope>();
    public DbSet<IamServiceDefinition> IamServiceDefinitions => Set<IamServiceDefinition>();
    public DbSet<IamPermissionSet> IamPermissionSets => Set<IamPermissionSet>();
    public DbSet<IamPermissionSetAssignment> IamPermissionSetAssignments => Set<IamPermissionSetAssignment>();
    public DbSet<IamWorkloadRole> IamWorkloadRoles => Set<IamWorkloadRole>();
    public DbSet<IamPermissionBoundary> IamPermissionBoundaries => Set<IamPermissionBoundary>();
    public DbSet<IamResourcePolicy> IamResourcePolicies => Set<IamResourcePolicy>();
    public DbSet<IamGroup> IamGroups => Set<IamGroup>();
    public DbSet<IamGroupMembership> IamGroupMemberships => Set<IamGroupMembership>();
    public new DbSet<IdentityUserClaim<Guid>> UserClaims => Set<IdentityUserClaim<Guid>>();

    // OpenIddict entity sets — need BOTH non-generic (store uses these) and generic <Guid> (EF model)
    // Non-generic sets are for OpenIddict 5.7.0 EF Core store access
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication> OpenIddictApplications => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization> OpenIddictAuthorizations => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope> OpenIddictScopes => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken> OpenIddictTokens => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectAuditMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectAuditMutation()
    {
        var mutation = ChangeTracker.Entries<AuditLog>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (mutation is not null)
            throw new InvalidOperationException("Audit logs are append-only and cannot be modified or deleted.");
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IamScope>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Scopes); entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.Kind, x.Key }).IsUnique();
            entity.HasIndex(x => x.ParentId);
        });
        builder.Entity<IamServiceDefinition>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Services); entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PermissionPrefix).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Owner).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });
        builder.Entity<IamPermissionSet>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.PermissionSets); entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PermissionsJson).HasMaxLength(16000).IsRequired();
            entity.Property(x => x.LifecycleStatus).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.ScopeId, x.Key }).IsUnique();
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamPermissionSetAssignment>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Assignments); entity.HasKey(x => x.Id);
            entity.Property(x => x.PrincipalType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.PrincipalId, x.ScopeId, x.Status });
            entity.HasIndex(x => new { x.PermissionSetId, x.PrincipalId, x.ScopeId }).IsUnique();
            entity.HasOne<IamPermissionSet>().WithMany().HasForeignKey(x => x.PermissionSetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamWorkloadRole>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.WorkloadRoles); entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Audience).HasMaxLength(256).IsRequired();
            entity.Property(x => x.TrustPolicyJson).HasMaxLength(16000).IsRequired();
            entity.Property(x => x.PermissionsJson).HasMaxLength(16000).IsRequired();
            entity.HasIndex(x => new { x.ScopeId, x.Key }).IsUnique();
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamPermissionBoundary>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Boundaries); entity.HasKey(x => x.Id);
            entity.Property(x => x.PrincipalType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AllowedPermissionsJson).HasMaxLength(16000).IsRequired();
            entity.Property(x => x.ResourceConstraintsJson).HasMaxLength(16000).IsRequired();
            entity.HasIndex(x => new { x.PrincipalId, x.PrincipalType, x.ScopeId }).IsUnique();
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamResourcePolicy>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.ResourcePolicies); entity.HasKey(x => x.Id);
            entity.Property(x => x.ServiceKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourcePattern).HasMaxLength(512).IsRequired();
            entity.Property(x => x.StatementsJson).HasMaxLength(32000).IsRequired();
            entity.Property(x => x.LifecycleStatus).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.ScopeId, x.ServiceKey, x.ResourcePattern }).IsUnique();
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamGroup>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Groups); entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired(); entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.ScopeId, x.Key }).IsUnique();
            entity.HasOne<IamScope>().WithMany().HasForeignKey(x => x.ScopeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IamGroupMembership>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.GroupMemberships); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            entity.HasOne<IamGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ──────────────────────────────────────────────
        // ASP.NET Identity table names (snake_case)
        // ──────────────────────────────────────────────
        builder.Entity<User>(entity => { entity.ToTable("asp_net_users"); });
        builder.Entity<Role>(entity => { entity.ToTable("asp_net_roles"); });
        builder.Entity<IdentityUserRole<Guid>>(entity => { entity.ToTable("asp_net_user_roles"); });
        builder.Entity<IdentityUserClaim<Guid>>(entity => { entity.ToTable("asp_net_user_claims"); });
        builder.Entity<IdentityUserLogin<Guid>>(entity => { entity.ToTable("asp_net_user_logins"); });
        builder.Entity<IdentityUserToken<Guid>>(entity => { entity.ToTable("asp_net_user_tokens"); });
        builder.Entity<IdentityRoleClaim<Guid>>(entity => { entity.ToTable("asp_net_role_claims"); });

        // ──────────────────────────────────────────────
        // User configuration
        // ──────────────────────────────────────────────
        builder.Entity<User>(entity =>
        {
            entity.Property(u => u.Id);
            entity.Property(u => u.UserName);
            entity.Property(u => u.NormalizedUserName);
            entity.Property(u => u.Email);
            entity.Property(u => u.NormalizedEmail);
            entity.Property(u => u.EmailConfirmed);
            entity.Property(u => u.PasswordHash);
            entity.Property(u => u.SecurityStamp);
            entity.Property(u => u.ConcurrencyStamp);
            entity.Property(u => u.PhoneNumber);
            entity.Property(u => u.PhoneNumberConfirmed);
            entity.Property(u => u.TwoFactorEnabled);
            entity.Property(u => u.LockoutEnd);
            entity.Property(u => u.LockoutEnabled);
            entity.Property(u => u.AccessFailedCount);
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.MiddleName).HasMaxLength(100);
            entity.Property(u => u.LicenseNumber).HasMaxLength(50);
            entity.Property(u => u.Specialty).HasMaxLength(200);
            entity.Property(u => u.PreferredLanguage).HasMaxLength(35).IsRequired().HasDefaultValue("vi-VN");
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastLoginAt);
            entity.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
            entity.Property(u => u.LockoutEnd);
            entity.Property(u => u.LastPasswordChangedAt);
            entity.Property(u => u.TrustedDeviceToken).HasMaxLength(256);
            // Stable, bounded admin listing order. Keep the primary key as a
            // tie-breaker so concurrent inserts cannot duplicate or skip rows.
            entity.HasIndex(u => new { u.CreatedAt, u.Id })
                .HasDatabaseName("ix_asp_net_users_created_at_id");
            entity.HasIndex(u => new { u.IsActive, u.CreatedAt, u.Id })
                .HasDatabaseName("ix_asp_net_users_active_created_at_id");
            entity.HasMany(u => u.FacilityMemberships)
                .WithOne(membership => membership.User)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserPasswordHistory>(entity =>
        {
            entity.ToTable("user_password_history");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(item => item.ChangedAt).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.ChangedAt });
            entity.HasOne(item => item.User)
                .WithMany(user => user.PasswordHistory)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SecuritySignalOutbox>(entity =>
        {
            entity.ToTable("security_signal_outbox");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(200).IsRequired();
            entity.Property(item => item.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.DispatchedAt, item.AvailableAt });
            entity.HasIndex(item => new { item.DispatchedAt, item.LeaseUntil, item.AvailableAt });
        });

        builder.Entity<UserClientCertificate>(entity =>
        {
            entity.ToTable("user_client_certificates");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Thumbprint).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(500);
            entity.HasIndex(item => new { item.Thumbprint, item.RevokedAt });
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DirectoryProvisioningOutbox>(entity =>
        {
            entity.ToTable("directory_provisioning_outbox");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Target).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Operation).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ResourceType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ResourceId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(item => item.ExternalId).HasMaxLength(512);
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.Target, item.CompletedAt, item.AvailableAt });
            entity.HasIndex(item => new { item.CompletedAt, item.LeaseUntil, item.AvailableAt });
            entity.HasIndex(item => new { item.Target, item.Operation, item.ResourceType, item.ResourceId, item.CreatedAt });
        });
        builder.Entity<DirectoryProvisioningBinding>(entity =>
        {
            entity.ToTable("directory_provisioning_bindings");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Target).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ResourceType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ResourceId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ExternalId).HasMaxLength(512).IsRequired();
            entity.HasIndex(item => new { item.Target, item.ResourceType, item.ResourceId }).IsUnique();
            entity.HasIndex(item => new { item.Target, item.ResourceType, item.ExternalId }).IsUnique();
        });

        builder.Entity<DevicePostureAssessment>(entity =>
        {
            entity.ToTable("device_posture_assessments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(256).IsRequired();
            entity.Property(item => item.ScopeId).HasMaxLength(100).HasDefaultValue(IdentityScope.Global);
            entity.Property(item => item.Provider).HasMaxLength(64).IsRequired();
            entity.Property(item => item.EvidenceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SignalsJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.PolicyVersion).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Decision).HasMaxLength(16).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(128);
            entity.HasIndex(item => new { item.UserId, item.DeviceId, item.ExpiresAt });
            entity.HasIndex(item => new { item.ScopeId, item.Provider, item.EvidenceHash }).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DevicePosturePolicy>(entity =>
        {
            entity.ToTable("device_posture_policies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(32);
            entity.Property(item => item.ScopeId).HasMaxLength(100).HasDefaultValue(IdentityScope.Global);
            entity.Property(item => item.Mode).HasMaxLength(16).IsRequired();
            entity.Property(item => item.ProvidersJson).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.RequiredSignalsJson).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(32).IsRequired();
            entity.Property(item => item.UpdatedBy).HasMaxLength(256);
        });

        builder.Entity<UserFacility>(entity =>
        {
            entity.ToTable("user_facilities");
            entity.HasKey(membership => new { membership.UserId, membership.FacilityId });
            entity.Property(membership => membership.FacilityId).HasMaxLength(100).IsRequired();
            entity.Property(membership => membership.IsPrimary).IsRequired();
            entity.Property(membership => membership.IsActive).IsRequired();
            entity.Property(membership => membership.CreatedAt).IsRequired();
            entity.HasIndex(membership => new { membership.UserId, membership.IsPrimary })
                .HasDatabaseName("ix_user_facilities_user_id_is_primary");
            entity.HasIndex(membership => new { membership.FacilityId, membership.IsActive })
                .HasDatabaseName("ix_user_facilities_facility_id_is_active");
        });

        // ──────────────────────────────────────────────
        // Role configuration
        // ──────────────────────────────────────────────
        builder.Entity<Role>(entity =>
        {
            entity.Property(r => r.Id);
            entity.Property(r => r.Name);
            entity.Property(r => r.NormalizedName);
            entity.Property(r => r.ConcurrencyStamp);
            entity.Property(r => r.Description).HasMaxLength(500);
            entity.Property(r => r.IsSystem).IsRequired().HasDefaultValue(false);
            entity.Property(r => r.Owner).HasMaxLength(128).IsRequired().HasDefaultValue("identity-service");
            entity.Property(r => r.AuthorizationVersion).IsRequired().HasDefaultValue(1);
            entity.Property(r => r.RiskTier).HasMaxLength(16).IsRequired().HasDefaultValue("standard");
            entity.Property(r => r.ReviewCadenceDays).IsRequired().HasDefaultValue(180);
            entity.Property(r => r.LifecycleStatus).HasMaxLength(16).IsRequired().HasDefaultValue("active");
            entity.Property(r => r.PublishedBy).HasMaxLength(256);
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasMany(r => r.RolePermissions)
                  .WithOne(rp => rp.Role)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(r => r.TemplateVersions)
                  .WithOne(v => v.Role)
                  .HasForeignKey(v => v.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoleTemplateVersion>(entity =>
        {
            entity.ToTable("role_template_versions");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).HasMaxLength(256).IsRequired();
            entity.Property(v => v.Description).HasMaxLength(500);
            entity.Property(v => v.Owner).HasMaxLength(128).IsRequired();
            entity.Property(v => v.RiskTier).HasMaxLength(16).IsRequired();
            entity.Property(v => v.LifecycleStatus).HasMaxLength(16).IsRequired();
            entity.Property(v => v.PermissionsJson).HasMaxLength(10000).IsRequired();
            entity.Property(v => v.CreatedBy).HasMaxLength(256);
            entity.Property(v => v.PublishedBy).HasMaxLength(256);
            entity.HasIndex(v => new { v.RoleId, v.Version }).IsUnique();
        });

        builder.Entity<AuthorizationPolicyDefinition>(entity =>
        {
            entity.ToTable("authorization_policy_definitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(128).IsRequired();
            entity.Property(item => item.LifecycleStatus).HasMaxLength(16).IsRequired();
            entity.Property(item => item.RulesJson).HasMaxLength(12000).IsRequired();
            entity.Property(item => item.CreatedBy).HasMaxLength(256);
            entity.Property(item => item.PublishedBy).HasMaxLength(256);
            entity.HasIndex(item => new { item.Key, item.Version }).IsUnique();
        });

        builder.Entity<AuthorizationPolicyBundleArtifact>(entity =>
        {
            entity.ToTable("authorization_policy_bundle_artifacts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SchemaVersion).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Hash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PoliciesJson).HasMaxLength(120000).IsRequired();
            entity.Property(item => item.Signature).HasMaxLength(12000).IsRequired();
            entity.Property(item => item.KeyId).HasMaxLength(256);
            entity.Property(item => item.CreatedBy).HasMaxLength(256).IsRequired();
            entity.HasIndex(item => item.Hash).IsUnique();
            entity.HasIndex(item => item.CreatedAt);
        });

        // ──────────────────────────────────────────────
        // Permission configuration
        // ──────────────────────────────────────────────
        builder.Entity<Permission>(entity =>
        {
            entity.ToTable(IdentityWorkbenchTableNames.Permissions);
            entity.HasKey(p => p.Code);
            entity.Property(p => p.Code).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Group).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.IsSystem).IsRequired().HasDefaultValue(true);
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasMany(p => p.RolePermissions)
                  .WithOne(rp => rp.Permission)
                  .HasForeignKey(rp => rp.PermissionCode)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.Group);
        });

        // ──────────────────────────────────────────────
        // RolePermission join entity configuration
        // ──────────────────────────────────────────────
        builder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionCode });
            entity.Property(rp => rp.RoleId);
            entity.Property(rp => rp.PermissionCode);

            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ──────────────────────────────────────────────
        // SystemSetting configuration
        // ──────────────────────────────────────────────
        builder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => new { s.ScopeId, s.Key });
            entity.Property(s => s.ScopeId).HasMaxLength(100).HasDefaultValue(IdentityScope.Global);
            entity.Property(s => s.Key).HasMaxLength(200).IsRequired();
            entity.Property(s => s.Value).HasMaxLength(2000).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.Category).HasMaxLength(100);
            entity.Property(s => s.UpdatedAt).IsRequired();
            entity.Property(s => s.UpdatedBy).HasMaxLength(100);

            entity.HasIndex(s => s.Category);
        });

        // ──────────────────────────────────────────────
        // AuditLog configuration
        // ──────────────────────────────────────────────
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(al => al.Id);
            entity.Property(al => al.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(al => al.UserId).HasMaxLength(100).IsRequired();
            entity.Property(al => al.UserName).HasMaxLength(200);
            entity.Property(al => al.Action).HasMaxLength(50).IsRequired();
            entity.Property(al => al.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(al => al.ResourceId).HasMaxLength(100);
            entity.Property(al => al.Details).HasMaxLength(2000);
            entity.Property(al => al.IpAddress).HasMaxLength(50);
            entity.Property(al => al.UserAgent).HasMaxLength(500);
            entity.Property(al => al.CorrelationId).HasMaxLength(128);
            entity.Property(al => al.Outcome).HasMaxLength(32);
            entity.Property(al => al.BeforeJson).HasMaxLength(8000);
            entity.Property(al => al.AfterJson).HasMaxLength(8000);
            entity.Property(al => al.Source).HasMaxLength(64);
            entity.Property(al => al.Timestamp).IsRequired();

            entity.HasIndex(al => al.UserId);
            entity.HasIndex(al => al.ResourceType);
            entity.HasIndex(al => al.Action);
            entity.HasIndex(al => al.Timestamp);
            entity.HasIndex(al => al.CorrelationId);
            entity.HasIndex(al => new { al.ResourceType, al.ResourceId, al.Timestamp })
                .HasDatabaseName("ix_audit_logs_resource_lookup");
            entity.HasIndex(al => new { al.UserId, al.Timestamp })
                .HasDatabaseName("ix_audit_logs_user_timeline");
        });

        // ──────────────────────────────────────────────
        // UserMfa configuration
        // ──────────────────────────────────────────────
        builder.Entity<UserMfa>(entity =>
        {
            entity.ToTable("user_mfa");
            entity.HasKey(m => m.UserId);
            // The value is encrypted before persistence, so its encoded form is
            // longer than the raw TOTP secret. Keep enough room for key rotation
            // and future encryption metadata without truncation failures.
            entity.Property(m => m.SecretKey).HasMaxLength(512).IsRequired();
            entity.Property(m => m.IsEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(m => m.EnrolledAt);
            entity.Property(m => m.RecoveryCodes);
            entity.Property(m => m.BackupCodesUsed).IsRequired().HasDefaultValue(0);
            entity.Property(m => m.CreatedAt).IsRequired();
            entity.Property(m => m.UpdatedAt).IsRequired();

            entity.HasOne(m => m.User)
                  .WithOne()
                  .HasForeignKey<UserMfa>(m => m.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ──────────────────────────────────────────────
        // SecurityEvent configuration
        // ──────────────────────────────────────────────
        builder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("security_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired().HasDefaultValue("info");
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.GeoCountry).HasMaxLength(100);
            entity.Property(e => e.Timestamp).IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.Timestamp);
        });

        // ──────────────────────────────────────────────
        // ClientConsent configuration
        // ──────────────────────────────────────────────
        builder.Entity<ClientConsent>(entity =>
        {
            entity.ToTable("client_consents");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(c => c.ClientId).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Scopes).IsRequired();
            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.ClientId);
            entity.HasIndex(c => new { c.UserId, c.ClientId }).IsUnique();
        });

        builder.Entity<TableView>(entity =>
        {
            entity.ToTable("user_table_views");
            entity.HasKey(view => view.Id);
            entity.Property(view => view.Resource).HasMaxLength(80).IsRequired();
            entity.Property(view => view.Name).HasMaxLength(80).IsRequired();
            entity.Property(view => view.PayloadJson).HasMaxLength(65536).IsRequired();
            entity.HasIndex(view => new { view.UserId, view.Resource, view.Name }).IsUnique();
        });

        builder.Entity<LocalizationResource>(entity =>
        {
            entity.ToTable("localization_resources");
            entity.HasKey(resource => new { resource.ScopeId, resource.Key });
            entity.Property(resource => resource.ScopeId).HasMaxLength(100).HasDefaultValue(IdentityScope.Global);
            entity.Property(resource => resource.Key).HasMaxLength(200).IsRequired();
            entity.Property(resource => resource.Description).HasMaxLength(500);
            entity.HasMany(resource => resource.Translations)
                .WithOne(translation => translation.Resource)
                .HasForeignKey(translation => new { translation.ScopeId, translation.ResourceKey })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LocalizationTranslation>(entity =>
        {
            entity.ToTable("localization_translations");
            entity.HasKey(translation => new { translation.ScopeId, translation.ResourceKey, translation.Locale });
            entity.Property(translation => translation.ScopeId).HasMaxLength(100).HasDefaultValue(IdentityScope.Global);
            entity.Property(translation => translation.ResourceKey).HasMaxLength(200).IsRequired();
            entity.Property(translation => translation.Locale).HasMaxLength(35).IsRequired();
            entity.Property(translation => translation.Value).HasMaxLength(4000).IsRequired();
            entity.HasIndex(translation => translation.Locale);
        });

        builder.Entity<LocalizationResource>().HasData(LocalizationSeedData.Resources.Select(resource => new
        {
            ScopeId = IdentityScope.Global,
            resource.Key,
            resource.Description
        }));
        builder.Entity<LocalizationTranslation>().HasData(LocalizationSeedData.Translations.Select(translation => new
        {
            ScopeId = IdentityScope.Global,
            translation.ResourceKey,
            translation.Locale,
            translation.Value
        }));

        builder.Entity<MobileDeviceRegistration>(entity =>
        {
            entity.ToTable("mobile_device_registrations");
            entity.HasKey(device => device.Id);
            entity.Property(device => device.UserId).HasMaxLength(200).IsRequired();
            entity.Property(device => device.Platform).HasMaxLength(20).IsRequired();
            entity.Property(device => device.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(device => device.TokenCiphertext).HasMaxLength(8192).IsRequired();
            entity.HasIndex(device => new { device.UserId, device.Platform, device.TokenHash }).IsUnique();
            entity.HasIndex(device => new { device.UserId, device.RevokedAt });
        });

        builder.Entity<MobileTelemetryEvent>(entity =>
        {
            entity.ToTable("mobile_telemetry_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(2000);
            entity.Property(item => item.Stack).HasMaxLength(8000);
            entity.Property(item => item.Route).HasMaxLength(500);
            entity.Property(item => item.AppVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Platform).HasMaxLength(20).IsRequired();
            entity.Property(item => item.MetadataJson).HasMaxLength(8000);
            entity.Property(item => item.CorrelationId).HasMaxLength(128);
            entity.HasIndex(item => new { item.EventType, item.CreatedAt });
        });

        builder.Entity<PushNotificationOutbox>(entity =>
        {
            entity.ToTable("push_notification_outbox");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.ProcessedAt, item.AvailableAt });
            entity.HasIndex(item => item.UserId);
        });

        builder.Entity<BreakGlassRequest>(entity =>
        {
            entity.ToTable("break_glass_requests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PermissionCode).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ResourceType).HasMaxLength(128);
            entity.Property(item => item.ResourceId).HasMaxLength(256);
            entity.Property(item => item.FacilityId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RequestedBy).HasMaxLength(256).IsRequired();
            entity.Property(item => item.ApprovedBy).HasMaxLength(256);
            entity.HasIndex(item => new { item.SubjectUserId, item.Status, item.ExpiresAt });
            entity.HasIndex(item => new { item.FacilityId, item.Status, item.ExpiresAt });
        });

        builder.Entity<AccessRequest>(entity =>
        {
            entity.ToTable("access_requests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RequestedBy).HasMaxLength(256).IsRequired();
            entity.Property(item => item.RoleIdsJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ApprovedBy).HasMaxLength(256);
            entity.HasIndex(item => new { item.SubjectUserId, item.Status, item.ExpiresAt });
        });

        builder.Entity<AccessReview>(entity =>
        {
            entity.ToTable("access_reviews");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reviewer).HasMaxLength(256).IsRequired();
            entity.Property(item => item.RoleIdsJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(2000);
            entity.HasIndex(item => new { item.SubjectUserId, item.Status, item.DueAt });
        });

        builder.Entity<SupportElevation>(entity =>
        {
            entity.ToTable("support_elevations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceTenant).HasMaxLength(128).IsRequired();
            entity.Property(item => item.TargetTenant).HasMaxLength(128).IsRequired();
            entity.Property(item => item.PermissionsJson).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RequestedBy).HasMaxLength(256);
            entity.Property(item => item.ApprovedBy).HasMaxLength(256);
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.HasIndex(item => new { item.OperatorUserId, item.TargetTenant, item.Status, item.ExpiresAt });
        });

        builder.Entity<InAppNotification>(entity =>
        {
            entity.ToTable("in_app_notifications");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.DataJson).HasMaxLength(8000);
            entity.HasIndex(item => new { item.UserId, item.ReadAt, item.CreatedAt });
        });

        builder.Entity<PushDeliveryAttempt>(entity =>
        {
            entity.ToTable("push_delivery_attempts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Platform).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(30).IsRequired();
            entity.Property(item => item.ErrorCode).HasMaxLength(200);
            entity.HasIndex(item => new { item.CreatedAt, item.Platform, item.Status });
            entity.HasIndex(item => item.OutboxId);
            entity.HasIndex(item => item.DeviceId);
        });

        builder.Entity<PasskeyCredential>(entity =>
        {
            entity.ToTable("passkey_credentials");
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.UserId).HasMaxLength(200).IsRequired();
            entity.Property(credential => credential.CredentialId).HasMaxLength(512).IsRequired();
            entity.Property(credential => credential.PublicKey).HasMaxLength(4096).IsRequired();
            entity.Property(credential => credential.SignatureCounter).IsRequired();
            entity.Property(credential => credential.CreatedAt).IsRequired();
            entity.Property(credential => credential.LastUsedAt).IsRequired();
            entity.HasIndex(credential => credential.CredentialId).IsUnique();
            entity.HasIndex(credential => new { credential.UserId, credential.CredentialId }).IsUnique();
        });

        // Configure OpenIddict tables (snake_case naming)
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication>(entity =>
        {
            entity.ToTable("openiddict_applications");
            entity.HasIndex(item => item.ClientId)
                .IsUnique()
                .HasDatabaseName("ix_openiddict_applications_client_id");
        });

        builder.Entity<AuthorizationChangeRequest>(entity =>
        {
            entity.ToTable("authorization_change_requests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ResourceType).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(128).IsRequired();
            entity.Property(item => item.RequestedBy).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ApprovedBy).HasMaxLength(256);
            entity.HasIndex(item => new { item.ResourceType, item.ResourceId, item.Action, item.Status });
            entity.HasIndex(item => new { item.Status, item.ExpiresAt });
        });
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization>(entity =>
        {
            entity.ToTable("openiddict_authorizations");
            entity.HasIndex(item => new { item.Subject, item.Status })
                .HasDatabaseName("ix_openiddict_authorizations_subject_status");
        });
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope>(entity =>
        {
            entity.ToTable("openiddict_scopes");
            entity.HasIndex(item => item.Name)
                .HasDatabaseName("ix_openiddict_scopes_name");
        });
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken>(entity =>
        {
            entity.ToTable("openiddict_tokens");
            entity.HasIndex(item => new { item.Subject, item.Status, item.ExpirationDate })
                .HasDatabaseName("ix_openiddict_tokens_subject_status_expiration");
            entity.HasIndex(item => new { item.Status, item.ExpirationDate })
                .HasDatabaseName("ix_openiddict_tokens_status_expiration");
        });

        HisHopeDataConventions.Apply(
            builder,
            typeof(User), typeof(Role), typeof(Permission), typeof(RolePermission),
            typeof(SystemSetting), typeof(UserMfa), typeof(ClientConsent), typeof(TableView),
            typeof(UserFacility), typeof(UserPasswordHistory), typeof(UserClientCertificate),
            typeof(RoleTemplateVersion), typeof(AuthorizationPolicyDefinition),
            typeof(AuthorizationPolicyBundleArtifact),
            typeof(IamScope), typeof(IamServiceDefinition), typeof(IamPermissionSet),
            typeof(IamPermissionSetAssignment), typeof(IamWorkloadRole),
            typeof(IamPermissionBoundary), typeof(IamResourcePolicy), typeof(IamGroup),
            typeof(IamGroupMembership));
    }
}
