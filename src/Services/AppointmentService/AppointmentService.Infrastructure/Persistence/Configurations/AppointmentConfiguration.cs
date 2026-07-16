using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.AppointmentService.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => AppointmentId.From(value));

        builder.Property(a => a.PatientId)
            .HasColumnName("PatientId")
            .IsRequired();

        builder.Property(a => a.ProviderId)
            .HasColumnName("ProviderId")
            .IsRequired();

        builder.Property(a => a.ScheduledDate)
            .HasColumnName("ScheduledDate")
            .IsRequired();

        builder.Property(a => a.StartTime)
            .HasColumnName("StartTime")
            .IsRequired();

        builder.Property(a => a.EndTime)
            .HasColumnName("EndTime")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion(
                s => s.Code,
                code => AppointmentStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasConversion(
                t => t.Code,
                code => AppointmentType.FromCode(code))
            .HasColumnName("Type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.Reason)
            .HasColumnName("Reason")
            .HasMaxLength(500);

        builder.Property(a => a.Notes)
            .HasColumnName("Notes")
            .HasMaxLength(2000);

        builder.Property(a => a.Location)
            .HasColumnName("Location")
            .HasMaxLength(200);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("UpdatedAt");

        builder.Property(a => a.CheckedInAt)
            .HasColumnName("CheckedInAt");

        builder.Property(a => a.CheckedOutAt)
            .HasColumnName("CheckedOutAt");

        builder.Property(a => a.CancelledAt)
            .HasColumnName("CancelledAt");

        builder.Property(a => a.CancellationReason)
            .HasColumnName("CancellationReason")
            .HasMaxLength(500);

        builder.HasIndex(a => a.PatientId)
            .HasDatabaseName("IX_Appointments_PatientId");

        builder.HasIndex(a => a.ProviderId)
            .HasDatabaseName("IX_Appointments_ProviderId");

        builder.HasIndex(a => a.ScheduledDate)
            .HasDatabaseName("IX_Appointments_ScheduledDate");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Appointments_Status");
    }
}
