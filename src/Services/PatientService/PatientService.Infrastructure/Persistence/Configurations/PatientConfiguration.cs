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
        });

        builder.Property(p => p.DateOfBirth).IsRequired();

        builder.OwnsOne(p => p.Gender, gender =>
        {
            gender.Property(g => g.Code).HasColumnName("Gender").HasMaxLength(10).IsRequired();
            gender.Ignore(g => g.Name);
        });

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

        builder.OwnsOne(p => p.BloodType, bt =>
        {
            bt.Property(b => b.Code).HasColumnName("BloodType").HasMaxLength(10);
            bt.Ignore(b => b.Name);
        });

        builder.OwnsOne(p => p.Race, race =>
        {
            race.Property(r => r.Code).HasColumnName("Race").HasMaxLength(20);
            race.Ignore(r => r.Name);
        });

        builder.OwnsOne(p => p.MaritalStatus, ms =>
        {
            ms.Property(m => m.Code).HasColumnName("MaritalStatus").HasMaxLength(10);
            ms.Ignore(m => m.Name);
        });

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
        builder.HasIndex("ContactInfo_Phone").IsUnique().HasFilter("\"IsActive\" = true");
        builder.HasIndex("Name_LastName", "Name_FirstName");
    }
}
