using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_BasePrice", "[BasePrice] >= 0");
            t.HasCheckConstraint("CK_Products_SalePrice", "[SalePrice] IS NULL OR ([SalePrice] >= 0 AND [SalePrice] <= [BasePrice])");
            t.HasCheckConstraint("CK_Products_SalePeriod", "[SaleStartAt] IS NULL OR [SaleEndAt] IS NULL OR [SaleStartAt] < [SaleEndAt]");
            t.HasCheckConstraint("CK_Products_Rating", "[AverageRating] >= 0 AND [AverageRating] <= 5");
        });

        builder.HasKey(x => x.ProductId).HasName("PK_Products");
        builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(100);
        builder.Property(x => x.Description);
        builder.Property(x => x.Material).HasMaxLength(100);
        builder.Property(x => x.GenderTarget).HasMaxLength(30);
        builder.Property(x => x.BasePrice).HasPrecision(18, 2);
        builder.Property(x => x.SalePrice).HasPrecision(18, 2);
        builder.Property(x => x.AverageRating).HasPrecision(3, 2).HasDefaultValueSql("0");
        builder.Property(x => x.ReviewCount).HasDefaultValue(0);
        builder.Property(x => x.SoldCount).HasDefaultValue(0);
        builder.Property(x => x.IsFeatured).HasDefaultValueSql("0").HasSentinel(false);
        builder.Property(x => x.IsActive).HasDefaultValueSql("1").HasSentinel(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .HasConstraintName("FK_Products_Categories")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UQ_Products_Slug");
        builder.HasIndex(x => x.CategoryId).HasFilter("[IsActive] = 1").HasDatabaseName("IX_Products_CategoryId");
        builder.HasIndex(x => x.CreatedAt).IsDescending().HasFilter("[IsActive] = 1").HasDatabaseName("IX_Products_CreatedAt");
        builder.HasIndex(x => x.SoldCount).IsDescending().HasFilter("[IsActive] = 1").HasDatabaseName("IX_Products_SoldCount");
    }
}
