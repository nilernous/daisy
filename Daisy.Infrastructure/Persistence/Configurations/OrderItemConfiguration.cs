using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
        });

        builder.HasKey(x => x.OrderItemId).HasName("PK_OrderItems");
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SizeSnapshot).HasMaxLength(20);
        builder.Property(x => x.ColorSnapshot).HasMaxLength(50);
        builder.Property(x => x.ImageUrlSnapshot).HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal)
            .HasPrecision(29, 2)
            .HasComputedColumnSql("[UnitPrice] * [Quantity]", stored: true);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
            .HasConstraintName("FK_OrderItems_Orders")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .HasConstraintName("FK_OrderItems_ProductVariants")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderId).HasDatabaseName("IX_OrderItems_OrderId");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("IX_OrderItems_VariantId");
    }
}
