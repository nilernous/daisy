using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_Stock", "[StockQuantity] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_LowStock", "[LowStockThreshold] >= 0");
        });

        builder.HasKey(x => x.VariantId).HasName("PK_ProductVariants");
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Size).HasMaxLength(20);
        builder.Property(x => x.Color).HasMaxLength(50);
        builder.Property(x => x.ColorHex).HasMaxLength(20);
        builder.Property(x => x.ExtraPrice).HasPrecision(18, 2).HasDefaultValueSql("0");
        builder.Property(x => x.StockQuantity).HasDefaultValue(0);
        builder.Property(x => x.LowStockThreshold).HasDefaultValue(5).HasSentinel(5);
        builder.Property(x => x.IsActive).HasDefaultValueSql("1").HasSentinel(true);
        builder.Ignore(x => x.InStock);
        builder.Ignore(x => x.IsLowStock);
        builder.Ignore(x => x.IsAvailable);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("FK_ProductVariants_Products")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("UQ_ProductVariants_Sku");
        builder.HasIndex(x => new { x.ProductId, x.Size, x.Color }).IsUnique().HasFilter(null).HasDatabaseName("UQ_ProductVariants_Option");
    }
}
