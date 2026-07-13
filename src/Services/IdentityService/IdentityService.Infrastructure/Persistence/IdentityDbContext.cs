using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

public class IdentityDbContext : IdentityDbContext<User, Role, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.MiddleName).HasMaxLength(100);
            entity.Property(u => u.LicenseNumber).HasMaxLength(50);
            entity.Property(u => u.Specialty).HasMaxLength(200);
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastLoginAt);
        });

        builder.Entity<Role>(entity =>
        {
            entity.Property(r => r.Description).HasMaxLength(500);
        });
    }
}
