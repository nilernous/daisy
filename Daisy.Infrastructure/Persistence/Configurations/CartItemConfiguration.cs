using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", t =>
        {
            t.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_CartItems_UserOrSession", "[UserId] IS NOT NULL OR [SessionId] IS NOT NULL");
        });

        builder.HasKey(x => x.CartItemId).HasName("PK_CartItems");
        builder.Property(x => x.SessionId).HasMaxLength(100);
        builder.Property(x => x.IsSelected).HasDefaultValueSql("1").HasSentinel(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_CartItems_Users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .HasConstraintName("FK_CartItems_ProductVariants")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.VariantId }).IsUnique()
            .HasFilter("[UserId] IS NOT NULL").HasDatabaseName("UX_CartItems_User_Variant");
        builder.HasIndex(x => new { x.SessionId, x.VariantId }).IsUnique()
            .HasFilter("[SessionId] IS NOT NULL").HasDatabaseName("UX_CartItems_Session_Variant");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("IX_CartItems_VariantId");
    }
}
