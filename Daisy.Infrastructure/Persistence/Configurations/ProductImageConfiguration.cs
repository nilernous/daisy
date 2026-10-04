using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.ImageId).HasName("PK_ProductImages");
        builder.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IsThumbnail).HasDefaultValueSql("0").HasSentinel(false);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("FK_ProductImages_Products")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .HasConstraintName("FK_ProductImages_ProductVariants")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId).HasDatabaseName("IX_ProductImages_ProductId");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("IX_ProductImages_VariantId");
    }
}
