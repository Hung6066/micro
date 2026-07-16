using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.LabService.Infrastructure.Persistence.Configurations;

public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("LabTests");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => LabTestId.From(value));

        builder.Property(t => t.LabOrderId)
            .HasConversion(
                id => id.Value,
                value => LabOrderId.From(value))
            .IsRequired();

        builder.Property(t => t.TestCode).HasMaxLength(20).IsRequired();
        builder.Property(t => t.TestName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.SpecimenType).HasMaxLength(100);

        builder.Property(t => t.Status)
            .HasConversion(
                s => s.Code,
                code => LabTestStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.OrderedAt).IsRequired();
        builder.Property(t => t.CollectedAt);
        builder.Property(t => t.CompletedAt);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.OwnsOne(t => t.Result, resultBuilder =>
        {
            resultBuilder.ToTable("LabResults");

            resultBuilder.Property<Guid>("Id").ValueGeneratedOnAdd();
            resultBuilder.HasKey("Id");

            resultBuilder.WithOwner();

            resultBuilder.Property(r => r.LabResultId)
                .HasConversion(
                    id => id.Value,
                    value => LabResultId.From(value))
                .HasColumnName("LabResultId");

            resultBuilder.Property(r => r.Value).HasMaxLength(500).IsRequired();
            resultBuilder.Property(r => r.Unit).HasMaxLength(50);
            resultBuilder.Property(r => r.ReferenceRange).HasMaxLength(100);

            resultBuilder.Property(r => r.AbnormalFlag)
                .HasConversion(
                    f => f != null ? f.Code : null,
                    code => code != null ? AbnormalFlag.FromCode(code) : null)
                .HasColumnName("AbnormalFlag")
                .HasMaxLength(20);

            resultBuilder.Property(r => r.ResultStatus)
                .HasConversion(
                    s => s.Code,
                    code => LabResultStatus.FromCode(code))
                .HasColumnName("ResultStatus")
                .HasMaxLength(20)
                .IsRequired();

            resultBuilder.Property(r => r.ResultedAt).IsRequired();
            resultBuilder.Property(r => r.PerformedBy).HasMaxLength(200);
            resultBuilder.Property(r => r.Notes).HasMaxLength(1000);
        });

        builder.HasIndex(t => t.LabOrderId);
    }
}
