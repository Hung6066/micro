using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.BillingService.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PaymentId.From(value))
            .HasColumnName("Id");

        builder.Property(p => p.InvoiceId)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value))
            .HasColumnName("InvoiceId");

        builder.Property(p => p.PatientId).HasColumnName("PatientId").IsRequired();
        builder.Property(p => p.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.PaymentDate).HasColumnName("PaymentDate").IsRequired();

        builder.Property(p => p.Method)
            .HasConversion(
                m => m.Code,
                code => PaymentMethod.FromCode(code))
            .HasColumnName("Method")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ReferenceNumber).HasColumnName("ReferenceNumber").HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion(
                s => s.Code,
                code => PaymentStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Notes).HasColumnName("Notes").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("CreatedAt").IsRequired();

        builder.HasIndex(p => p.InvoiceId);
        builder.HasIndex(p => p.PatientId);
    }
}
