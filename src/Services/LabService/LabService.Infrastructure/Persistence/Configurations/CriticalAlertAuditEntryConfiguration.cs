using His.Hope.LabService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.LabService.Infrastructure.Persistence.Configurations;

public class CriticalAlertAuditEntryConfiguration : IEntityTypeConfiguration<CriticalAlertAuditEntry>
{
    public void Configure(EntityTypeBuilder<CriticalAlertAuditEntry> builder)
    {
        builder.ToTable("CriticalAlertAuditEntries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CriticalAlertId).HasColumnName("criticalalertid").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ActorUserId).HasColumnName("actoruserid").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ActorDisplayName).HasColumnName("actordisplayname").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(e => e.OccurredAt).HasColumnName("occurredat").IsRequired();

        builder.HasIndex(e => e.CriticalAlertId);
        builder.HasIndex(e => e.OccurredAt);
    }
}
