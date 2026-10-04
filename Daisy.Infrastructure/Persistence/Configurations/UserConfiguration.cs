using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            t.HasCheckConstraint("CK_Users_Role", "[Role] IN (N'Customer', N'Staff', N'Admin')");
            t.HasCheckConstraint("CK_Users_Status", "[Status] IN (N'Active', N'Locked')");
        });

        builder.HasKey(x => x.UserId).HasName("PK_Users");
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(Domain.Enums.UserRole.Customer).HasSentinel(Domain.Enums.UserRole.Customer);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(Domain.Enums.UserStatus.Active).HasSentinel(Domain.Enums.UserStatus.Active);
        builder.Property(x => x.Gender).HasMaxLength(20);
        builder.Property(x => x.ResetPasswordToken).HasMaxLength(255);
        builder.Property(x => x.RefreshToken).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
        builder.Ignore(x => x.IsLocked);
    }
}
