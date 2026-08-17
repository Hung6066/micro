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

        builder.Property(a => a.FacilityId).HasColumnName("facility_id").HasMaxLength(100);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("appointment_id")
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
            .HasColumnName("Status")
            .HasConversion(
                s => s.Code,
                code => AppointmentStatus.FromCode(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasColumnName("Type")
            .HasConversion(
                t => t.Code,
                code => AppointmentType.FromCode(code))
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

        builder.HasIndex(a => a.FacilityId)
            .HasDatabaseName("IX_Appointments_FacilityId");

        builder.HasIndex(a => new { a.PatientId, a.ScheduledDate })
            .HasDatabaseName("IX_Appointments_Patient_Scheduled_Id");
        builder.HasIndex(a => new { a.ProviderId, a.ScheduledDate })
            .HasDatabaseName("IX_Appointments_Provider_Scheduled_Id");
        builder.HasIndex(a => new { a.FacilityId, a.Status, a.ScheduledDate })
            .HasDatabaseName("IX_Appointments_Facility_Status_Scheduled_Id");
    }
}
