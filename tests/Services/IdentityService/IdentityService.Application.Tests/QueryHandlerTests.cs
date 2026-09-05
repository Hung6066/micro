using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.UseCases.Roles.Queries;
using His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;
using His.Hope.IdentityService.Application.UseCases.Settings.Commands;
using His.Hope.IdentityService.Application.UseCases.Settings.Queries;
using His.Hope.IdentityService.Application.UseCases.Users.Queries;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class QueryHandlerTests
{
    [Fact]
    public async Task GetUsers_applies_search_role_status_sort_and_maps_roles()
    {
        await using var db = TestApplicationDbContext.Create();
        var clinician = IdentityTestData.User("clinician", "clinician@example.test", firstName: "An", lastName: "Nguyen");
        var inactive = IdentityTestData.User("inactive", "inactive@example.test", isActive: false, firstName: "Binh", lastName: "Tran");
        var role = IdentityTestData.Role("Clinician", "Clinical access");
        db.Users.AddRange(clinician, inactive);
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = clinician.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var result = await new GetUsersQueryHandler(db).Handle(
            new GetUsersQuery(Search: " an ", Role: "Clinician", IsActive: true, Sort: "username:asc"),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Should().Match<UserDetailDto>(u =>
            u.UserName == "clinician" && u.FullName == "Nguyen An" && u.Roles.Contains("Clinician"));
    }

    [Fact]
    public async Task GetUsers_returns_empty_page_when_page_is_after_result_set()
    {
        await using var db = TestApplicationDbContext.Create();
        db.Users.Add(new User { UserName = "one", Email = "one@example.test", FirstName = "One", LastName = "User" });
        await db.SaveChangesAsync();

        var result = await new GetUsersQueryHandler(db).Handle(new GetUsersQuery(Page: 2, PageSize: 1), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsers_rejects_unbounded_deep_pages()
    {
        await using var db = TestApplicationDbContext.Create();

        var act = () => new GetUsersQueryHandler(db).Handle(
            new GetUsersQuery(Page: 10_001), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("page");
    }

    [Fact]
    public async Task GetRoles_filters_sorts_and_projects_permissions()
    {
        await using var db = TestApplicationDbContext.Create();
        var role = IdentityTestData.Role("Clinician", "Clinical access");
        var permission = IdentityTestData.Permission("patients.read", "Read patients", "patients");
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = permission.Code, Role = role, Permission = permission });
        await db.SaveChangesAsync();

        var result = await new GetRolesQueryHandler(db).Handle(new GetRolesQuery(Search: "Clinical", Sort: "name:asc"), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Permissions.Should().ContainSingle()
            .Which.Code.Should().Be("patients.read");
    }

    [Fact]
    public async Task GetRoles_rejects_page_sizes_above_platform_limit()
    {
        await using var db = TestApplicationDbContext.Create();

        var act = () => new GetRolesQueryHandler(db).Handle(
            new GetRolesQuery(PageSize: 101), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("pageSize");
    }

    [Fact]
    public async Task GetPermissions_orders_by_group_then_code_and_maps_system_flag()
    {
        await using var db = TestApplicationDbContext.Create();
        db.Permissions.AddRange(
            IdentityTestData.Permission("z.read", "Z read", "users"),
            IdentityTestData.Permission("a.read", "A read", "users"),
            new Permission { Code = "system.audit", Name = "Audit", Group = "audit", IsSystem = true });
        await db.SaveChangesAsync();

        var result = await new GetPermissionsQueryHandler(db).Handle(new GetPermissionsQuery(), CancellationToken.None);

        result.Select(x => x.Code).Should().Equal("system.audit", "a.read", "z.read");
        result.Single(x => x.Code == "system.audit").IsSystem.Should().BeTrue();
    }

    [Fact]
    public async Task GetRoleById_returns_null_for_unknown_role_and_maps_known_role()
    {
        await using var db = TestApplicationDbContext.Create();
        var role = IdentityTestData.Role("Auditor", "Read-only");
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var handler = new GetRoleByIdQueryHandler(db);

        (await handler.Handle(new GetRoleByIdQuery(Guid.NewGuid()), CancellationToken.None)).Should().BeNull();
        (await handler.Handle(new GetRoleByIdQuery(role.Id), CancellationToken.None))!.Name.Should().Be("Auditor");
    }

    [Fact]
    public async Task Settings_queries_order_and_return_null_for_missing_key()
    {
        await using var db = TestApplicationDbContext.Create();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "z.key", Value = "z", Category = "general" },
            new SystemSetting { Key = "a.key", Value = "a", Category = "general" });
        await db.SaveChangesAsync();

        var list = await new GetSettingsQueryHandler(db).Handle(new GetSettingsQuery(), CancellationToken.None);
        var missing = await new GetSettingByKeyQueryHandler(db).Handle(new GetSettingByKeyQuery("missing"), CancellationToken.None);

        list.Select(x => x.Key).Should().Equal("a.key", "z.key");
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Settings_queries_prefer_scope_value_and_keep_global_fallback()
    {
        await using var db = TestApplicationDbContext.Create();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "security.mfa", Value = "global", Category = "security", ScopeId = IdentityScope.Global },
            new SystemSetting { Key = "security.mfa", Value = "tenant", Category = "security", ScopeId = "tenant-a" },
            new SystemSetting { Key = "security.session", Value = "global-only", Category = "security", ScopeId = IdentityScope.Global });
        await db.SaveChangesAsync();

        var list = await new GetSettingsQueryHandler(db)
            .Handle(new GetSettingsQuery("tenant-a"), CancellationToken.None);
        var scoped = await new GetSettingByKeyQueryHandler(db)
            .Handle(new GetSettingByKeyQuery("security.mfa", "tenant-a"), CancellationToken.None);

        list.Should().HaveCount(2);
        list.Single(x => x.Key == "security.mfa").Value.Should().Be("tenant");
        list.Single(x => x.Key == "security.session").Value.Should().Be("global-only");
        scoped.Should().NotBeNull();
        scoped!.Value.Should().Be("tenant");
        scoped.ScopeId.Should().Be("tenant-a");
    }

    [Fact]
    public async Task UpdateSetting_and_bulk_update_are_isolated_by_scope()
    {
        await using var db = TestApplicationDbContext.Create();
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "security.mfa", Value = "global", Description = "global description",
            ScopeId = IdentityScope.Global
        });
        await db.SaveChangesAsync();

        var update = await new UpdateSettingCommandHandler(db).Handle(
            new UpdateSettingCommand("security.mfa", "tenant", "tenant description", "operator", "tenant-a"),
            CancellationToken.None);
        var bulk = await new BulkUpdateSettingsCommandHandler(db).Handle(
            new BulkUpdateSettingsCommand([new BulkUpdateSettingItem("security.session", "short")], "operator", "tenant-a"),
            CancellationToken.None);

        update.Value.Should().Be("tenant");
        update.ScopeId.Should().Be("tenant-a");
        bulk.Should().ContainSingle().Which.ScopeId.Should().Be("tenant-a");
        db.SystemSettings.Should().HaveCount(3);
        db.SystemSettings.Single(x => x.Key == "security.mfa" && x.ScopeId == IdentityScope.Global)
            .Value.Should().Be("global");
    }

    [Fact]
    public async Task GetSettingByKey_maps_all_setting_fields()
    {
        await using var db = TestApplicationDbContext.Create();
        var updatedAt = DateTime.UtcNow.AddMinutes(-5);
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "security.mfa",
            Value = "required",
            Description = "MFA policy",
            Category = "security",
            UpdatedAt = updatedAt,
            UpdatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var result = await new GetSettingByKeyQueryHandler(db)
            .Handle(new GetSettingByKeyQuery("security.mfa"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Key.Should().Be("security.mfa");
        result.Value.Should().Be("required");
        result.Description.Should().Be("MFA policy");
        result.Category.Should().Be("security");
        result.UpdatedBy.Should().Be("admin");
        result.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task UpdateSetting_creates_then_updates_existing_setting_without_erasing_description()
    {
        await using var db = TestApplicationDbContext.Create();
        var handler = new UpdateSettingCommandHandler(db);

        var created = await handler.Handle(
            new UpdateSettingCommand("security.mfa", "required", "MFA policy", "admin"),
            CancellationToken.None);
        var updated = await handler.Handle(
            new UpdateSettingCommand("security.mfa", "optional", null, "operator"),
            CancellationToken.None);

        created.Value.Should().Be("required");
        updated.Value.Should().Be("optional");
        updated.Description.Should().Be("MFA policy");
        updated.UpdatedBy.Should().Be("operator");
        db.SystemSettings.Should().ContainSingle(x => x.Key == "security.mfa");
    }

    [Fact]
    public async Task BulkUpdateSettings_upserts_all_items_and_returns_current_values()
    {
        await using var db = TestApplicationDbContext.Create();
        db.SystemSettings.Add(new SystemSetting { Key = "existing", Value = "old", Description = "keep" });
        await db.SaveChangesAsync();

        var result = await new BulkUpdateSettingsCommandHandler(db).Handle(
            new BulkUpdateSettingsCommand(
                [
                    new BulkUpdateSettingItem("existing", "new"),
                    new BulkUpdateSettingItem("created", "value")
                ],
                "admin"),
            CancellationToken.None);

        result.Select(x => x.Key).Should().Equal("existing", "created");
        result[0].Value.Should().Be("new");
        result[0].Description.Should().Be("keep");
        result[1].Value.Should().Be("value");
        db.SystemSettings.Should().HaveCount(2);
    }

    [Fact]
    public async Task Audit_log_query_filters_dates_and_sorts_descending()
    {
        await using var db = TestApplicationDbContext.Create();
        var baseline = DateTime.UtcNow.AddMinutes(-10);
        db.AuditLogs.AddRange(
            new AuditLog { UserId = "u1", UserName = "one", Action = "read", ResourceType = "User", Timestamp = baseline },
            new AuditLog { UserId = "u1", UserName = "one", Action = "update", ResourceType = "User", Timestamp = baseline.AddMinutes(5) },
            new AuditLog { UserId = "u2", UserName = "two", Action = "delete", ResourceType = "Role", Timestamp = baseline.AddMinutes(8) });
        await db.SaveChangesAsync();

        var result = await new GetAuditLogsQueryHandler(db).Handle(
            new GetAuditLogsQuery(UserId: "u1", DateFrom: baseline.AddMinutes(-1), DateTo: baseline.AddMinutes(6), Sort: "timestamp:desc"),
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(x => x.Action).Should().Equal("update", "read");
    }

    [Fact]
    public async Task Audit_log_query_supports_action_and_resource_sorting_with_paging()
    {
        await using var db = TestApplicationDbContext.Create();
        var baseline = DateTime.UtcNow.AddMinutes(-5);
        db.AuditLogs.AddRange(
            new AuditLog { UserId = "u1", Action = "z-read", ResourceType = "Alpha", Timestamp = baseline },
            new AuditLog { UserId = "u1", Action = "a-write", ResourceType = "Zeta", Timestamp = baseline.AddMinutes(1) },
            new AuditLog { UserId = "u1", Action = "m-update", ResourceType = "Beta", Timestamp = baseline.AddMinutes(2) });
        await db.SaveChangesAsync();

        var actionAscending = await new GetAuditLogsQueryHandler(db).Handle(
            new GetAuditLogsQuery(Page: 1, PageSize: 2, Sort: "action:asc"), CancellationToken.None);
        actionAscending.TotalCount.Should().Be(3);
        actionAscending.Items.Select(x => x.Action).Should().Equal("a-write", "m-update");

        var resourceDescending = await new GetAuditLogsQueryHandler(db).Handle(
            new GetAuditLogsQuery(Page: 2, PageSize: 1, Sort: "resourceType:desc"), CancellationToken.None);
        resourceDescending.TotalCount.Should().Be(3);
        resourceDescending.Items.Should().ContainSingle();
        resourceDescending.Items[0].ResourceType.Should().Be("Beta");
    }

    [Fact]
    public async Task Audit_log_by_id_maps_existing_entry_and_returns_null_for_unknown()
    {
        await using var db = TestApplicationDbContext.Create();
        var log = new AuditLog
        {
            Id = Guid.NewGuid(), UserId = "user-1", UserName = "operator", Action = "READ",
            ResourceType = "Patient", ResourceId = "patient-1", Details = "viewed",
            IpAddress = "127.0.0.1", UserAgent = "test", Timestamp = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var handler = new GetAuditLogByIdQueryHandler(db);

        (await handler.Handle(new GetAuditLogByIdQuery(Guid.NewGuid()), CancellationToken.None)).Should().BeNull();
        var result = await handler.Handle(new GetAuditLogByIdQuery(log.Id), CancellationToken.None);
        result.Should().NotBeNull();
        result!.UserId.Should().Be("user-1");
        result.ResourceId.Should().Be("patient-1");
    }

    [Fact]
    public async Task GetRoles_applies_tenant_membership_filter_and_descending_sort()
    {
        await using var db = TestApplicationDbContext.Create();
        var matchingUser = IdentityTestData.User("tenant-user", "tenant@example.test");
        var otherUser = IdentityTestData.User("other-user", "other@example.test");
        var matchingRole = IdentityTestData.Role("TenantRole", "Tenant role");
        var otherRole = IdentityTestData.Role("OtherRole", "Other role");
        db.Users.AddRange(matchingUser, otherUser);
        db.Roles.AddRange(matchingRole, otherRole);
        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = matchingUser.Id, RoleId = matchingRole.Id },
            new IdentityUserRole<Guid> { UserId = otherUser.Id, RoleId = otherRole.Id });
        db.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = matchingUser.Id, ClaimType = "tenant_membership", ClaimValue = "Group-HQ"
        });
        await db.SaveChangesAsync();

        var result = await new GetRolesQueryHandler(db).Handle(
            new GetRolesQuery(Search: null, Sort: "name:desc", TenantMembershipKeys: [" group-hq "]),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("TenantRole");
    }

    [Fact]
    public async Task GetRoles_supports_description_and_created_at_sorting_and_paging()
    {
        await using var db = TestApplicationDbContext.Create();
        db.Roles.AddRange(
            IdentityTestData.Role("Alpha", "Z description"),
            IdentityTestData.Role("Beta", "A description"));
        await db.SaveChangesAsync();

        var byDescription = await new GetRolesQueryHandler(db).Handle(
            new GetRolesQuery(Page: 1, PageSize: 1, Sort: "description:asc"), CancellationToken.None);
        var byCreatedAt = await new GetRolesQueryHandler(db).Handle(
            new GetRolesQuery(Page: 1, PageSize: 20, Sort: "createdAt:desc"), CancellationToken.None);

        byDescription.TotalCount.Should().Be(2);
        byDescription.Items.Should().ContainSingle().Which.Name.Should().Be("Beta");
        byCreatedAt.Items.Should().HaveCount(2);
    }
}

internal sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    private TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionCode });
        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionCode);
        modelBuilder.Entity<IdentityUserRole<Guid>>().HasKey(x => new { x.UserId, x.RoleId });
        // Scope-aware settings use the composite identity that the production
        // store uses; otherwise global and tenant overrides cannot coexist in
        // the test context and scope precedence is untestable.
        modelBuilder.Entity<SystemSetting>().HasKey(x => new { x.Key, x.ScopeId });
        modelBuilder.Entity<UserMfa>().HasKey(x => x.UserId);
        modelBuilder.Entity<UserFacility>().HasKey(x => new { x.UserId, x.FacilityId });
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserMfa> UserMfas => Set<UserMfa>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<IdentityUserRole<Guid>> UserRoles => Set<IdentityUserRole<Guid>>();
    public DbSet<IdentityUserClaim<Guid>> UserClaims => Set<IdentityUserClaim<Guid>>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<MobileDeviceRegistration> MobileDeviceRegistrations => Set<MobileDeviceRegistration>();
    public DbSet<MobileTelemetryEvent> MobileTelemetryEvents => Set<MobileTelemetryEvent>();
    public DbSet<PushNotificationOutbox> PushNotificationOutbox => Set<PushNotificationOutbox>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<PushDeliveryAttempt> PushDeliveryAttempts => Set<PushDeliveryAttempt>();
    public DbSet<UserFacility> UserFacilities => Set<UserFacility>();
    public DbSet<BreakGlassRequest> BreakGlassRequests => Set<BreakGlassRequest>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AccessReview> AccessReviews => Set<AccessReview>();
    public DbSet<RoleTemplateVersion> RoleTemplateVersions => Set<RoleTemplateVersion>();
    public DbSet<AuthorizationPolicyDefinition> AuthorizationPolicies => Set<AuthorizationPolicyDefinition>();
    public DbSet<AuthorizationPolicyBundleArtifact> AuthorizationPolicyBundles => Set<AuthorizationPolicyBundleArtifact>();
    public DbSet<AuthorizationChangeRequest> AuthorizationChangeRequests => Set<AuthorizationChangeRequest>();
    public DbSet<IamScope> IamScopes => Set<IamScope>();
    public DbSet<IamServiceDefinition> IamServiceDefinitions => Set<IamServiceDefinition>();
    public DbSet<IamPermissionSet> IamPermissionSets => Set<IamPermissionSet>();
    public DbSet<IamPermissionSetAssignment> IamPermissionSetAssignments => Set<IamPermissionSetAssignment>();
    public DbSet<IamWorkloadRole> IamWorkloadRoles => Set<IamWorkloadRole>();
    public DbSet<IamPermissionBoundary> IamPermissionBoundaries => Set<IamPermissionBoundary>();
    public DbSet<IamGroupMembership> IamGroupMemberships => Set<IamGroupMembership>();
    public DbSet<IamResourcePolicy> IamResourcePolicies => Set<IamResourcePolicy>();

    public static TestApplicationDbContext Create() => new(
        new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
