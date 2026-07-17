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
        builder.ToTable("lab_orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => LabOrderId.From(value))
            .HasColumnName("id");

        builder.Property(o => o.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(o => o.ProviderId).HasColumnName("provider_id").IsRequired();
        builder.Property(o => o.EncounterId).HasColumnName("encounter_id");

        builder.Property(o => o.OrderDate).HasColumnName("order_date").IsRequired();

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
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsMany(o => o.RequestedTests, testBuilder =>
        {
            testBuilder.ToTable("lab_tests");
            testBuilder.WithOwner().HasForeignKey("LabOrderId");

            testBuilder.HasKey(t => t.Id);

            testBuilder.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => LabTestId.From(value))
                .HasColumnName("id");

            testBuilder.Property(t => t.TestCode).HasColumnName("test_code").HasMaxLength(20).IsRequired();
            testBuilder.Property(t => t.TestName).HasColumnName("test_name").HasMaxLength(200).IsRequired();
            testBuilder.Property(t => t.SpecimenType).HasColumnName("specimen_type").HasMaxLength(100);

            testBuilder.Property(t => t.Status)
                .HasConversion(
                    s => s.Code,
                    code => LabTestStatus.FromCode(code))
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired();

            testBuilder.Property(t => t.OrderedAt).HasColumnName("ordered_at").IsRequired();
            testBuilder.Property(t => t.CollectedAt).HasColumnName("collected_at");
            testBuilder.Property(t => t.CompletedAt).HasColumnName("completed_at");
            testBuilder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            testBuilder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

            testBuilder.OwnsOne(t => t.Result, resultBuilder =>
            {
                resultBuilder.ToTable("lab_results");

                resultBuilder.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnName("id");
                resultBuilder.HasKey("Id");

                resultBuilder.WithOwner();
                resultBuilder.Property("LabTestId").HasColumnName("lab_test_id");

                resultBuilder.Property(r => r.LabResultId)
                    .HasConversion(
                        id => id.Value,
                        value => LabResultId.From(value))
                    .HasColumnName("lab_result_id");

                resultBuilder.Property(r => r.Value).HasColumnName("value").HasMaxLength(500).IsRequired();
                resultBuilder.Property(r => r.Unit).HasColumnName("unit").HasMaxLength(50);
                resultBuilder.Property(r => r.ReferenceRange).HasColumnName("reference_range").HasMaxLength(100);

                resultBuilder.Property(r => r.AbnormalFlag)
                    .HasConversion(
                        f => f != null ? f.Code : null,
                        code => code != null ? AbnormalFlag.FromCode(code) : null)
                    .HasColumnName("abnormal_flag")
                    .HasMaxLength(20);

                resultBuilder.Property(r => r.ResultStatus)
                    .HasConversion(
                        s => s.Code,
                        code => LabResultStatus.FromCode(code))
                    .HasColumnName("result_status")
                    .HasMaxLength(20)
                    .IsRequired();

                resultBuilder.Property(r => r.ResultedAt).HasColumnName("resulted_at").IsRequired();
                resultBuilder.Property(r => r.PerformedBy).HasColumnName("performed_by").HasMaxLength(200);
                resultBuilder.Property(r => r.Notes).HasColumnName("notes").HasMaxLength(1000);
            });

            testBuilder.Property("LabOrderId").HasColumnName("lab_order_id");
            testBuilder.HasIndex("LabOrderId");
        });

        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.ProviderId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.OrderDate);
    }
}
