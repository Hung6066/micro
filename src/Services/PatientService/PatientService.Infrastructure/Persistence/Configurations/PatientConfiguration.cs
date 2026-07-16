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
        builder.ToTable("Patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PatientId.From(value));

        builder.OwnsOne(p => p.Name, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("FirstName").HasMaxLength(100).IsRequired();
            name.Property(n => n.LastName).HasColumnName("LastName").HasMaxLength(100).IsRequired();
            name.Property(n => n.MiddleName).HasColumnName("MiddleName").HasMaxLength(100);
            name.HasIndex(n => new { n.LastName, n.FirstName });
        });

        builder.Property(p => p.DateOfBirth).IsRequired();

        builder.Property(p => p.Gender)
            .HasConversion(
                g => g == null ? null : g.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.Gender.FromCode(code))
            .HasColumnName("Gender")
            .HasMaxLength(10)
            .IsRequired();

        builder.OwnsOne(p => p.ContactInfo, contact =>
        {
            contact.Property(c => c.Phone).HasColumnName("Phone").HasMaxLength(20).IsRequired();
            contact.Property(c => c.Email).HasColumnName("Email").HasMaxLength(200);
        });

        builder.OwnsOne(p => p.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200).IsRequired();
            address.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("City").HasMaxLength(100).IsRequired();
            address.Property(a => a.Province).HasColumnName("Province").HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(100).IsRequired();
        });

        builder.Property(p => p.BloodType)
            .HasConversion(
                bt => bt == null ? null : bt.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.BloodType.FromCode(code))
            .HasColumnName("BloodType")
            .HasMaxLength(10);

        // Simple enumeration types stored as code strings
        var convertEnum = new ValueConverter<His.Hope.SharedKernel.Domain.Common.Enumeration<His.Hope.PatientService.Domain.ValueObjects.Race>, string>(
            e => e.Code,
            s => s == null ? null : His.Hope.PatientService.Domain.ValueObjects.Race.FromCode(s));

        builder.Property(p => p.Race)
            .HasConversion(
                r => r == null ? null : r.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.Race.FromCode(code))
            .HasColumnName("Race")
            .HasMaxLength(20);

        builder.Property(p => p.MaritalStatus)
            .HasConversion(
                ms => ms == null ? null : ms.Code,
                code => code == null ? null : His.Hope.PatientService.Domain.ValueObjects.MaritalStatus.FromCode(code))
            .HasColumnName("MaritalStatus")
            .HasMaxLength(10);

        builder.Property(p => p.InsuranceId).HasMaxLength(50);
        builder.Property(p => p.NationalId).HasMaxLength(50);
        builder.Property(p => p.Occupation).HasMaxLength(200);
        builder.Property(p => p.EmergencyContactName).HasMaxLength(200);
        builder.Property(p => p.EmergencyContactPhone).HasMaxLength(20);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.HasMany(p => p.Allergies)
            .WithOne()
            .HasForeignKey("PatientId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Conditions)
            .WithOne()
            .HasForeignKey("PatientId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.IsActive);
        builder.OwnsOne(p => p.ContactInfo, contact =>
        {
            contact.HasIndex(c => c.Phone).IsUnique().HasFilter("\"IsActive\" = true");
        });
    }
}
