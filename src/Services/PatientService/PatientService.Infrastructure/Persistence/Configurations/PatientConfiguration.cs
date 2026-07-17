using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace His.Hope.PatientService.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("patient_id")
            .HasConversion(
                id => id.Value,
                value => PatientId.From(value));

        builder.OwnsOne(p => p.Name, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            name.Property(n => n.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            name.Property(n => n.MiddleName).HasColumnName("middle_name").HasMaxLength(100);
            name.HasIndex(n => new { n.LastName, n.FirstName });
        });

        builder.Property(p => p.DateOfBirth).HasColumnName("date_of_birth").IsRequired();

        builder.Property(p => p.Gender)
            .HasConversion(
                g => g == null ? null : g.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.Gender.FromCode(code))
            .HasColumnName("gender")
            .HasMaxLength(10)
            .IsRequired();

        builder.OwnsOne(p => p.ContactInfo, contact =>
        {
            contact.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(20).IsRequired();
            contact.Property(c => c.Email).HasColumnName("email").HasMaxLength(200);
        });

        builder.OwnsOne(p => p.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("street").HasMaxLength(200).IsRequired();
            address.Property(a => a.District).HasColumnName("district").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("city").HasMaxLength(100).IsRequired();
            address.Property(a => a.Province).HasColumnName("province").HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
        });

        builder.Property(p => p.BloodType)
            .HasConversion(
                bt => bt == null ? null : bt.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.BloodType.FromCode(code))
            .HasColumnName("blood_type")
            .HasMaxLength(10);

        // Simple enumeration types stored as code strings
        var convertEnum = new ValueConverter<His.Hope.SharedKernel.Domain.Common.Enumeration<His.Hope.PatientService.Domain.ValueObjects.Race>, string>(
            e => e.Code,
            s => s == null ? null : His.Hope.PatientService.Domain.ValueObjects.Race.FromCode(s));

        builder.Property(p => p.Race)
            .HasConversion(
                r => r == null ? null : r.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.Race.FromCode(code))
            .HasColumnName("race")
            .HasMaxLength(20);

        builder.Property(p => p.MaritalStatus)
            .HasConversion(
                ms => ms == null ? null : ms.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.MaritalStatus.FromCode(code))
            .HasColumnName("marital_status")
            .HasMaxLength(10);

        builder.Property(p => p.InsuranceId).HasColumnName("insurance_id").HasMaxLength(50);
        builder.Property(p => p.NationalId).HasColumnName("national_id").HasMaxLength(50);
        builder.Property(p => p.Occupation).HasColumnName("occupation").HasMaxLength(200);
        builder.Property(p => p.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(200);
        builder.Property(p => p.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(20);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(p => p.Allergies)
            .WithOne()
            .HasForeignKey("PatientId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Conditions)
            .WithOne()
            .HasForeignKey("PatientId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.IsActive).HasDatabaseName("ix_patients_is_active");
        builder.OwnsOne(p => p.ContactInfo, contact =>
        {
            contact.HasIndex(c => c.Phone).IsUnique().HasFilter("\"is_active\" = true").HasDatabaseName("ix_patients_phone");
        });
    }
}
