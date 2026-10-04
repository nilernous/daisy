using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_OrderStatus", "[OrderStatus] IN (N'Pending', N'Confirmed', N'Shipping', N'Completed', N'Canceled')");
            t.HasCheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] IN (N'COD', N'Online')");
            t.HasCheckConstraint("CK_Orders_PaymentStatus", "[PaymentStatus] IN (N'Unpaid', N'Paid', N'Failed', N'Refunded')");
            t.HasCheckConstraint("CK_Orders_Amounts", "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [ShippingFee] >= 0 AND [TotalAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_PaymentExpiresAt", "[PaymentExpiresAt] IS NULL OR [PaymentMethod] = N'Online'");
        });

        builder.HasKey(x => x.OrderId).HasName("PK_Orders");
        builder.Property(x => x.OrderCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ReceiverPhone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShippingAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(50)
            .HasDefaultValue(OrderStatus.Pending).HasSentinel(OrderStatus.Pending);
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(PaymentMethod.COD).HasSentinel(PaymentMethod.COD);
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(PaymentStatus.Unpaid).HasSentinel(PaymentStatus.Unpaid);
        builder.Property(x => x.TransactionCode).HasMaxLength(100);
        builder.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).HasDefaultValueSql("0");
        builder.Property(x => x.ShippingFee).HasPrecision(18, 2).HasDefaultValueSql("0");
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.CustomerNote).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
        builder.Ignore(x => x.CanCancel);
        builder.Ignore(x => x.CanAcceptTransfer);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_Orders_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Voucher)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.VoucherId)
            .HasConstraintName("FK_Orders_Vouchers")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderCode).IsUnique().HasDatabaseName("UQ_Orders_OrderCode");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_Orders_UserId");
        builder.HasIndex(x => new { x.OrderStatus, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_Orders_OrderStatus");
        builder.HasIndex(x => x.TransactionCode).IsUnique()
            .HasFilter("[TransactionCode] IS NOT NULL").HasDatabaseName("UX_Orders_TransactionCode");
        builder.HasIndex(x => x.PaymentExpiresAt)
            .HasFilter("[PaymentExpiresAt] IS NOT NULL AND [PaymentStatus] = N'Unpaid' AND [OrderStatus] = N'Pending'")
            .HasDatabaseName("IX_Orders_PaymentExpiresAt");
        builder.HasIndex(x => x.VoucherId).HasDatabaseName("IX_Orders_VoucherId");
    }
}
