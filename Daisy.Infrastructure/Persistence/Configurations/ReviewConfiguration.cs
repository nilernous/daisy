using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));

        builder.HasKey(x => x.ReviewId).HasName("PK_Reviews");
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.IsVisible).HasDefaultValueSql("1").HasSentinel(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_Reviews_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("FK_Reviews_Products")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OrderItem)
            .WithOne(x => x.Review)
            .HasForeignKey<Review>(x => x.OrderItemId)
            .HasConstraintName("FK_Reviews_OrderItems")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderItemId).IsUnique().HasDatabaseName("UQ_Reviews_OrderItem");
        builder.HasIndex(x => x.ProductId).HasFilter("[IsVisible] = 1").HasDatabaseName("IX_Reviews_ProductId");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_Reviews_UserId");
    }
}
