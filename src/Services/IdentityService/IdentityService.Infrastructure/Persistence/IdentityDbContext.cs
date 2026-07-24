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

    // OpenIddict entity sets — use generic types matching OpenIddict 5.x EF Core stores
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication<Guid>> OpenIddictApplications => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication<Guid>>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization<Guid>> OpenIddictAuthorizations => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization<Guid>>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope<Guid>> OpenIddictScopes => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope<Guid>>();
    public DbSet<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken<Guid>> OpenIddictTokens => Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken<Guid>>();

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
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastLoginAt);
            entity.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
            entity.Property(u => u.LockoutEnd);
            entity.Property(u => u.LastPasswordChangedAt);
            entity.Property(u => u.TrustedDeviceToken).HasMaxLength(256);
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
            entity.Property(m => m.SecretKey).HasMaxLength(100).IsRequired();
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

        // Configure OpenIddict tables (snake_case naming)
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication<Guid>>(entity =>
            entity.ToTable("openiddict_applications"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreAuthorization<Guid>>(entity =>
            entity.ToTable("openiddict_authorizations"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreScope<Guid>>(entity =>
            entity.ToTable("openiddict_scopes"));
        builder.Entity<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreToken<Guid>>(entity =>
            entity.ToTable("openiddict_tokens"));
    }
}
