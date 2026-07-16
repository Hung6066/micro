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
                value => InvoiceId.From(value));

        builder.Property(i => i.PatientId).IsRequired();
        builder.Property(i => i.EncounterId);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(i => i.InvoiceDate).IsRequired();
        builder.Property(i => i.DueDate);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.Property(i => i.Status)
            .HasConversion(
                s => s.Code,
                code => InvoiceStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.SubTotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.DiscountAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.PaidAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);

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
