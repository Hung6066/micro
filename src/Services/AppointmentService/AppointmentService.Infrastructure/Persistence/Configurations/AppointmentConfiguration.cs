using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.AppointmentService.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("appointment_id")
            .HasConversion(
                id => id.Value,
                value => AppointmentId.From(value));

        builder.Property(a => a.PatientId)
            .IsRequired();

        builder.Property(a => a.ProviderId)
            .IsRequired();

        builder.Property(a => a.ScheduledDate)
            .IsRequired();

        builder.Property(a => a.StartTime)
            .IsRequired();

        builder.Property(a => a.EndTime)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion(
                s => s.Code,
                code => AppointmentStatus.FromCode(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasConversion(
                t => t.Code,
                code => AppointmentType.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.Reason)
            .HasMaxLength(500);

        builder.Property(a => a.Notes)
            .HasMaxLength(2000);

        builder.Property(a => a.Location)
            .HasMaxLength(200);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.Property(a => a.CheckedInAt)
            .HasColumnName("check_in_at");

        builder.Property(a => a.CheckedOutAt)
            .HasColumnName("check_out_at");

        builder.Property(a => a.CancelledAt)
            .HasColumnName("canceled_at");

        builder.Property(a => a.CancellationReason)
            .HasColumnName("cancel_reason")
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
