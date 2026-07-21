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
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value))
            .HasColumnName("Id");

        builder.Property(i => i.PatientId).HasColumnName("PatientId").IsRequired();
        builder.Property(i => i.EncounterId).HasColumnName("EncounterId");
        builder.Property(i => i.InvoiceNumber).HasColumnName("InvoiceNumber").HasMaxLength(50).IsRequired();
        builder.Property(i => i.InvoiceDate).HasColumnName("InvoiceDate").IsRequired();
        builder.Property(i => i.DueDate).HasColumnName("DueDate");
        builder.Property(i => i.Notes).HasColumnName("Notes").HasMaxLength(1000);

        builder.Property(i => i.Status)
            .HasConversion(
                s => s.Code,
                code => InvoiceStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.SubTotal).HasColumnName("SubTotal").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.TaxAmount).HasColumnName("TaxAmount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.DiscountAmount).HasColumnName("DiscountAmount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.PaidAmount).HasColumnName("PaidAmount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("CreatedAt").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("UpdatedAt");

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
