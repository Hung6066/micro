using His.Hope.PatientService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PatientService.Infrastructure.Persistence.Configurations;

public class MedicalConditionConfiguration : IEntityTypeConfiguration<MedicalCondition>
{
    public void Configure(EntityTypeBuilder<MedicalCondition> builder)
    {
        builder.ToTable("MedicalConditions");

        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.ConditionName).HasMaxLength(300).IsRequired();
        builder.Property(mc => mc.Icd10Code).HasMaxLength(20);
        builder.Property(mc => mc.OnsetDate);
        builder.Property(mc => mc.ResolvedDate);
        builder.Property(mc => mc.IsChronic).IsRequired();
        builder.Property(mc => mc.Notes).HasMaxLength(1000);
        builder.Property(mc => mc.RecordedDate).IsRequired();
        builder.Property(mc => mc.IsActive).IsRequired();

        builder.HasIndex(mc => mc.Icd10Code);
    }
}
