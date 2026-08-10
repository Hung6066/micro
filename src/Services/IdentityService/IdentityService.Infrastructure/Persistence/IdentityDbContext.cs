using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
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
    public DbSet<ClientConsent> ClientConsents => Set<ClientConsent>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<MobileDeviceRegistration> MobileDeviceRegistrations => Set<MobileDeviceRegistration>();
    public DbSet<MobileTelemetryEvent> MobileTelemetryEvents => Set<MobileTelemetryEvent>();
    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();
    public DbSet<PushNotificationOutbox> PushNotificationOutbox => Set<PushNotificationOutbox>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<PushDeliveryAttempt> PushDeliveryAttempts => Set<PushDeliveryAttempt>();
    public DbSet<UserFacility> UserFacilities => Set<UserFacility>();
    public DbSet<LocalizationResource> LocalizationResources => Set<LocalizationResource>();
    public DbSet<LocalizationTranslation> LocalizationTranslations => Set<LocalizationTranslation>();

    // OpenIddict entity sets — need BOTH non-generic (store uses these) and generic <Guid> (EF model)
    // Non-generic sets are for OpenIddict 5.7.0 EF Core store access
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication> OpenIddictApplications => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization> OpenIddictAuthorizations => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope> OpenIddictScopes => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken> OpenIddictTokens => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
            entity.HasMany(u => u.FacilityMemberships)
                .WithOne(membership => membership.User)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // TODO: Enable after running migration to add previous_password_hashes column
            // entity.Property(u => u.PreviousPasswordHashes);
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
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasMany(r => r.RolePermissions)
                  .WithOne(rp => rp.Role)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ──────────────────────────────────────────────
        // Permission configuration
        // ──────────────────────────────────────────────
        builder.Entity<Permission>(entity =>
        {
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
            entity.HasKey(s => s.Key);
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
            entity.Property(al => al.Timestamp).IsRequired();

            entity.HasIndex(al => al.UserId);
            entity.HasIndex(al => al.ResourceType);
            entity.HasIndex(al => al.Action);
            entity.HasIndex(al => al.Timestamp);
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
            entity.ToTable("openiddict_consents");
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
            entity.ToTable("admin_table_views");
            entity.HasKey(view => view.Id);
            entity.Property(view => view.Resource).HasMaxLength(80).IsRequired();
            entity.Property(view => view.Name).HasMaxLength(80).IsRequired();
            entity.Property(view => view.PayloadJson).HasMaxLength(65536).IsRequired();
            entity.HasIndex(view => new { view.UserId, view.Resource, view.Name }).IsUnique();
        });

        builder.Entity<LocalizationResource>(entity =>
        {
            entity.ToTable("localization_resources");
            entity.HasKey(resource => resource.Key);
            entity.Property(resource => resource.Key).HasMaxLength(200).IsRequired();
            entity.Property(resource => resource.Description).HasMaxLength(500);
            entity.HasMany(resource => resource.Translations)
                .WithOne(translation => translation.Resource)
                .HasForeignKey(translation => translation.ResourceKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LocalizationTranslation>(entity =>
        {
            entity.ToTable("localization_translations");
            entity.HasKey(translation => new { translation.ResourceKey, translation.Locale });
            entity.Property(translation => translation.ResourceKey).HasMaxLength(200).IsRequired();
            entity.Property(translation => translation.Locale).HasMaxLength(35).IsRequired();
            entity.Property(translation => translation.Value).HasMaxLength(4000).IsRequired();
            entity.HasIndex(translation => translation.Locale);
        });

        builder.Entity<LocalizationResource>().HasData(LocalizationSeedData.Resources);
        builder.Entity<LocalizationTranslation>().HasData(LocalizationSeedData.Translations);

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
            entity.ToTable("openiddict_applications"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization>(entity =>
            entity.ToTable("openiddict_authorizations"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope>(entity =>
            entity.ToTable("openiddict_scopes"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken>(entity =>
            entity.ToTable("openiddict_tokens"));
    }
}
