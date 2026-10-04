using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers", t =>
        {
            t.HasCheckConstraint("CK_Vouchers_DiscountType", "[DiscountType] IN (N'Percent', N'Fixed')");
            t.HasCheckConstraint("CK_Vouchers_DiscountValue", "[DiscountValue] > 0 AND ([DiscountType] = N'Fixed' OR [DiscountValue] <= 100)");
            t.HasCheckConstraint("CK_Vouchers_UsedCount", "[UsageLimit] IS NULL OR [UsedCount] <= [UsageLimit]");
            t.HasCheckConstraint("CK_Vouchers_Period", "[StartAt] < [EndAt]");
        });

        builder.HasKey(x => x.VoucherId).HasName("PK_Vouchers");
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.MinOrderAmount).HasPrecision(18, 2).HasDefaultValueSql("0");
        builder.Property(x => x.UsedCount).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValueSql("1").HasSentinel(true);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Vouchers_Code");
    }
}
