using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.BillingService.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value))
            .HasColumnName("id");

        builder.Property(i => i.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(i => i.EncounterId).HasColumnName("encounter_id");
        builder.Property(i => i.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(50).IsRequired();
        builder.Property(i => i.InvoiceDate).HasColumnName("invoice_date").IsRequired();
        builder.Property(i => i.DueDate).HasColumnName("due_date");
        builder.Property(i => i.Notes).HasColumnName("notes").HasMaxLength(1000);

        builder.Property(i => i.Status)
            .HasConversion(
                s => s.Code,
                code => InvoiceStatus.FromCode(code))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.SubTotal).HasColumnName("sub_total").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.TaxAmount).HasColumnName("tax_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.DiscountAmount).HasColumnName("discount_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.PaidAmount).HasColumnName("paid_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.PatientId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InvoiceDate);
    }
}
