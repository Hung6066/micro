using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.BillingService.Infrastructure.Persistence.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(li => li.Id);

        builder.Property(li => li.Id)
            .HasConversion(
                id => id.Value,
                value => InvoiceLineItemId.From(value));

        builder.Property(li => li.InvoiceId)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value));

        builder.Property(li => li.Description).HasMaxLength(500).IsRequired();
        builder.Property(li => li.Quantity).IsRequired();
        builder.Property(li => li.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(li => li.ItemCode).HasMaxLength(50);

        builder.Property(li => li.ItemType)
            .HasConversion(
                t => t != null ? t.Code : null,
                code => code != null ? InvoiceLineItemType.FromCode(code) : null)
            .HasColumnName("ItemType")
            .HasMaxLength(20);

        builder.Property(li => li.CreatedAt).IsRequired();

        builder.HasIndex(li => li.InvoiceId);
    }
}
