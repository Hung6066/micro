using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.LabService.Infrastructure.Persistence.Configurations;

public class CriticalAlertConfiguration : IEntityTypeConfiguration<CriticalAlert>
{
    public void Configure(EntityTypeBuilder<CriticalAlert> builder)
    {
        builder.ToTable("CriticalAlerts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.LabOrderId).HasColumnName("laborderid").IsRequired();
        builder.Property(a => a.LabTestId).HasColumnName("labtestid").IsRequired();
        builder.Property(a => a.LabResultId).HasColumnName("labresultid").IsRequired();
        builder.Property(a => a.RuleId).HasColumnName("ruleid");

        builder.Property(a => a.TriggerType)
            .HasConversion(
                triggerType => triggerType.Code,
                code => CriticalAlertTriggerType.FromCode(code))
            .HasColumnName("triggertype")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion(
                status => status.Code,
                code => CriticalAlertStatus.FromCode(code))
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
        builder.Property(a => a.ResultValue).HasColumnName("resultvalue").HasMaxLength(500).IsRequired();
        builder.Property(a => a.ResultUnit).HasColumnName("resultunit").HasMaxLength(50);
        builder.Property(a => a.ThresholdValue).HasColumnName("thresholdvalue");
        builder.Property(a => a.CreatedAt).HasColumnName("createdat").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updatedat").IsRequired();
        builder.Property(a => a.AcknowledgedAt).HasColumnName("acknowledgedat");
        builder.Property(a => a.AcknowledgedByUserId).HasColumnName("acknowledgedbyuserid").HasMaxLength(100);
        builder.Property(a => a.AcknowledgedByDisplayName).HasColumnName("acknowledgedbydisplayname").HasMaxLength(200);
        builder.Property(a => a.ResolvedAt).HasColumnName("resolvedat");
        builder.Property(a => a.ResolvedByUserId).HasColumnName("resolvedbyuserid").HasMaxLength(100);
        builder.Property(a => a.ResolvedByDisplayName).HasColumnName("resolvedbydisplayname").HasMaxLength(200);

        builder.Navigation(a => a.AuditEntries)
            .HasField("_auditEntries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.AuditEntries)
            .WithOne()
            .HasForeignKey(e => e.CriticalAlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.LabOrderId, a.LabTestId });
        builder.HasIndex(a => a.Status);
    }
}
