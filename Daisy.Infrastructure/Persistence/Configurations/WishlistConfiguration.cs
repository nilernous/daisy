using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> builder)
    {
        builder.ToTable("Wishlists");
        builder.HasKey(x => new { x.UserId, x.ProductId }).HasName("PK_Wishlists");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_Wishlists_Users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("FK_Wishlists_Products")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProductId).HasDatabaseName("IX_Wishlists_ProductId");
    }
}
