using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.BillingService.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PaymentId.From(value))
            .HasColumnName("id");

        builder.Property(p => p.InvoiceId)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value))
            .HasColumnName("invoice_id");

        builder.Property(p => p.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.PaymentDate).HasColumnName("payment_date").IsRequired();

        builder.Property(p => p.Method)
            .HasConversion(
                m => m.Code,
                code => PaymentMethod.FromCode(code))
            .HasColumnName("method")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion(
                s => s.Code,
                code => PaymentStatus.FromCode(code))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(p => p.InvoiceId);
        builder.HasIndex(p => p.PatientId);
    }
}
