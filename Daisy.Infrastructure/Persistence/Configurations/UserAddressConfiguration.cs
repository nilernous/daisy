using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("UserAddresses");
        builder.HasKey(x => x.AddressId).HasName("PK_UserAddresses");
        builder.Property(x => x.ReceiverName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ReceiverPhone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Province).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Ward).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StreetAddress).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IsDefault).HasDefaultValueSql("0").HasSentinel(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
        builder.Ignore(x => x.FullAddress);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Addresses)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_UserAddresses_Users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId, "IX_UserAddresses_UserId");
        builder.HasIndex(x => x.UserId, "UX_UserAddresses_Default").IsUnique().HasFilter("[IsDefault] = 1");
    }
}
