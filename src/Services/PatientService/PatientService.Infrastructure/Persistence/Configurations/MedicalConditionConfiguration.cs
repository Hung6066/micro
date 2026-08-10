using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PatientService.Infrastructure.Persistence.Configurations;

public class MedicalConditionConfiguration : IEntityTypeConfiguration<MedicalCondition>
{
    public void Configure(EntityTypeBuilder<MedicalCondition> builder)
    {
        builder.ToTable("medical_conditions");

        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.Id).HasColumnName("condition_id");

        builder.Property<PatientId>("PatientId")
            .HasConversion(
                id => id.Value,
                value => PatientId.From(value))
            .HasColumnName("patient_id");

        builder.Property(mc => mc.ConditionName).HasColumnName("condition_name").HasMaxLength(300).IsRequired();
        builder.Property(mc => mc.Icd10Code).HasColumnName("icd10_code").HasMaxLength(20);
        builder.Property(mc => mc.OnsetDate).HasColumnName("onset_date");
        builder.Property(mc => mc.ResolvedDate).HasColumnName("resolved_date");
        builder.Property(mc => mc.IsChronic).HasColumnName("is_chronic").IsRequired();
        builder.Property(mc => mc.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(mc => mc.RecordedDate).HasColumnName("recorded_date").IsRequired();
        builder.Property(mc => mc.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(mc => mc.Icd10Code);
    }
}
