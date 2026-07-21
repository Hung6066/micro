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
                value => InvoiceLineItemId.From(value))
            .HasColumnName("Id");

        builder.Property(li => li.InvoiceId)
            .HasConversion(
                id => id.Value,
                value => InvoiceId.From(value))
            .HasColumnName("InvoiceId");

        builder.Property(li => li.Description).HasColumnName("Description").HasMaxLength(500).IsRequired();
        builder.Property(li => li.Quantity).HasColumnName("Quantity").IsRequired();
        builder.Property(li => li.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(li => li.ItemCode).HasColumnName("ItemCode").HasMaxLength(50);

        builder.Property(li => li.ItemType)
            .HasConversion(
                t => t != null ? t.Code : null,
                code => code != null ? InvoiceLineItemType.FromCode(code) : null)
            .HasColumnName("ItemType")
            .HasMaxLength(20);

        builder.Property(li => li.CreatedAt).HasColumnName("CreatedAt").IsRequired();

        builder.HasIndex(li => li.InvoiceId);
    }
}
