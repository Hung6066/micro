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
                value => LabOrderId.From(value))
            .HasColumnName("id");

        builder.Property(o => o.PatientId).HasColumnName("patientid").IsRequired();
        builder.Property(o => o.ProviderId).HasColumnName("providerid").IsRequired();
        builder.Property(o => o.EncounterId).HasColumnName("encounterid");

        builder.Property(o => o.OrderDate).HasColumnName("orderdate").IsRequired();

        builder.Property(o => o.Status)
            .HasConversion(
                s => s.Code,
                code => LabOrderStatus.FromCode(code))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Priority)
            .HasConversion(
                p => p.Code,
                code => LabOrderPriority.FromCode(code))
            .HasColumnName("priority")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(o => o.CreatedAt).HasColumnName("createdat").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updatedat");

        builder.Navigation(o => o.RequestedTests)
            .HasField("_tests")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(o => o.RequestedTests, testBuilder =>
        {
            testBuilder.ToTable("LabTests");
            testBuilder.WithOwner().HasForeignKey("LabOrderId");

            testBuilder.HasKey(t => t.Id);

            testBuilder.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => LabTestId.From(value))
                .HasColumnName("id");

            testBuilder.Property(t => t.TestCode).HasColumnName("testcode").HasMaxLength(20).IsRequired();
            testBuilder.Property(t => t.TestName).HasColumnName("testname").HasMaxLength(200).IsRequired();
            testBuilder.Property(t => t.SpecimenType).HasColumnName("specimentype").HasMaxLength(100);

            testBuilder.Property(t => t.Status)
                .HasConversion(
                    s => s.Code,
                    code => LabTestStatus.FromCode(code))
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired();

            testBuilder.Property(t => t.OrderedAt).HasColumnName("orderedat").IsRequired();
            testBuilder.Property(t => t.CollectedAt).HasColumnName("collectedat");
            testBuilder.Property(t => t.CompletedAt).HasColumnName("completedat");
            testBuilder.Property(t => t.CreatedAt).HasColumnName("createdat").IsRequired();
            testBuilder.Property(t => t.UpdatedAt).HasColumnName("updatedat");

            testBuilder.OwnsOne(t => t.Result, resultBuilder =>
            {
                resultBuilder.ToTable("LabResults");

                resultBuilder.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnName("id");
                resultBuilder.HasKey("Id");

                resultBuilder.WithOwner();
                resultBuilder.Property("LabTestId").HasColumnName("labtestid");

                resultBuilder.Property(r => r.LabResultId)
                    .HasConversion(
                        id => id.Value,
                        value => LabResultId.From(value))
                    .HasColumnName("labresultid");

                resultBuilder.Property(r => r.Value).HasColumnName("value").HasMaxLength(500).IsRequired();
                resultBuilder.Property(r => r.Unit).HasColumnName("unit").HasMaxLength(50);
                resultBuilder.Property(r => r.ReferenceRange).HasColumnName("referencerange").HasMaxLength(100);

                resultBuilder.Property(r => r.AbnormalFlag)
                    .HasConversion(
                        f => f != null ? f.Code : null,
                        code => code != null ? AbnormalFlag.FromCode(code) : null)
                    .HasColumnName("abnormalflag")
                    .HasMaxLength(20);

                resultBuilder.Property(r => r.ResultStatus)
                    .HasConversion(
                        s => s.Code,
                        code => LabResultStatus.FromCode(code))
                    .HasColumnName("resultstatus")
                    .HasMaxLength(20)
                    .IsRequired();

                resultBuilder.Property(r => r.ResultedAt).HasColumnName("resultedat").IsRequired();
                resultBuilder.Property(r => r.PerformedBy).HasColumnName("performedby").HasMaxLength(200);
                resultBuilder.Property(r => r.Notes).HasColumnName("notes").HasMaxLength(1000);
            });

            testBuilder.Property("LabOrderId").HasColumnName("laborderid");
            testBuilder.HasIndex("LabOrderId");
        });

        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.ProviderId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.OrderDate);
    }
}
