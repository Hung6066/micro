using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.LabService.Infrastructure.Persistence.Configurations;

public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.ToTable("LabOrders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => LabOrderId.From(value));

        builder.Property(o => o.PatientId).IsRequired();
        builder.Property(o => o.ProviderId).IsRequired();
        builder.Property(o => o.EncounterId);

        builder.Property(o => o.OrderDate).IsRequired();

        builder.Property(o => o.Status)
            .HasConversion(
                s => s.Code,
                code => LabOrderStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Priority)
            .HasConversion(
                p => p.Code,
                code => LabOrderPriority.FromCode(code))
            .HasColumnName("Priority")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);

        builder.OwnsMany(o => o.RequestedTests, testBuilder =>
        {
            testBuilder.ToTable("LabTests");
            testBuilder.WithOwner().HasForeignKey("LabOrderId");

            testBuilder.HasKey(t => t.Id);

            testBuilder.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => LabTestId.From(value));

            testBuilder.Property(t => t.TestCode).HasMaxLength(20).IsRequired();
            testBuilder.Property(t => t.TestName).HasMaxLength(200).IsRequired();
            testBuilder.Property(t => t.SpecimenType).HasMaxLength(100);

            testBuilder.Property(t => t.Status)
                .HasConversion(
                    s => s.Code,
                    code => LabTestStatus.FromCode(code))
                .HasColumnName("Status")
                .HasMaxLength(20)
                .IsRequired();

            testBuilder.Property(t => t.OrderedAt).IsRequired();
            testBuilder.Property(t => t.CollectedAt);
            testBuilder.Property(t => t.CompletedAt);
            testBuilder.Property(t => t.CreatedAt).IsRequired();
            testBuilder.Property(t => t.UpdatedAt);

            testBuilder.OwnsOne(t => t.Result, resultBuilder =>
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

            testBuilder.HasIndex("LabOrderId");
        });

        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.ProviderId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.OrderDate);
    }
}
