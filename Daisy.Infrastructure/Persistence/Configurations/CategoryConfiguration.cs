using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.CategoryId).HasName("PK_Categories");
        builder.Property(x => x.CategoryName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValueSql("1").HasSentinel(true);

        // Self-referencing hierarchy (BR-13).
        builder.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .HasConstraintName("FK_Categories_Parent")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UQ_Categories_Slug");
        builder.HasIndex(x => x.ParentCategoryId).HasDatabaseName("IX_Categories_ParentCategoryId");
    }
}
