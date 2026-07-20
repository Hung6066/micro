using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.LabService.Infrastructure.Persistence.Configurations;

public class CriticalAlertRuleConfiguration : IEntityTypeConfiguration<CriticalAlertRule>
{
    public void Configure(EntityTypeBuilder<CriticalAlertRule> builder)
    {
        builder.ToTable("CriticalAlertRules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TestCode).HasColumnName("testcode").HasMaxLength(20).IsRequired();
        builder.Property(r => r.TestName).HasColumnName("testname").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Unit).HasColumnName("unit").HasMaxLength(50);
        builder.Property(r => r.LowCriticalValue).HasColumnName("lowcriticalvalue");
        builder.Property(r => r.HighCriticalValue).HasColumnName("highcriticalvalue");
        builder.Property(r => r.IsActive).HasColumnName("isactive").IsRequired();
        builder.Property(r => r.CreatedByUserId).HasColumnName("createdbyuserid").HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreatedByDisplayName).HasColumnName("createdbydisplayname").HasMaxLength(200).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("createdat").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updatedat");

        builder.HasIndex(r => r.TestCode);
        builder.HasIndex(r => new { r.TestCode, r.IsActive });
    }
}
