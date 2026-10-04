using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daisy.Infrastructure.Persistence.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions", t =>
        {
            t.HasCheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_PaymentTransactions_MatchResult", "[MatchResult] IN (N'Matched', N'NoOrder', N'Underpaid', N'OrderNotPayable')");
            t.HasCheckConstraint("CK_PaymentTransactions_Resolution", "[Resolution] IS NULL OR ([Resolution] IN (N'Applied', N'Refunded', N'Ignored') AND [MatchResult] <> N'Matched')");
            t.HasCheckConstraint(
                "CK_PaymentTransactions_Resolved",
                "([Resolution] IS NULL AND [ResolvedAt] IS NULL AND [ResolvedByUserId] IS NULL) OR ([Resolution] IS NOT NULL AND [ResolvedAt] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL)");
            t.HasCheckConstraint("CK_PaymentTransactions_Matched", "[MatchResult] <> N'Matched' OR [OrderId] IS NOT NULL");
            t.HasCheckConstraint("CK_PaymentTransactions_Applied", "[Resolution] IS NULL OR [Resolution] <> N'Applied' OR [OrderId] IS NOT NULL");
        });

        builder.HasKey(x => x.PaymentTransactionId).HasName("PK_PaymentTransactions");
        builder.Property(x => x.BankReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.TransferContent).HasMaxLength(500);
        builder.Property(x => x.ReceivedAt).HasDefaultValueSql("SYSDATETIME()");
        builder.Property(x => x.MatchResult).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ResolutionNote).HasMaxLength(500);
        builder.Ignore(x => x.NeedsReview);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .HasConstraintName("FK_PaymentTransactions_Orders")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedBy)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .HasConstraintName("FK_PaymentTransactions_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BankReference).IsUnique().HasDatabaseName("UQ_PaymentTransactions_BankReference");
        builder.HasIndex(x => x.OrderId).HasFilter("[OrderId] IS NOT NULL").HasDatabaseName("IX_PaymentTransactions_OrderId");
        builder.HasIndex(x => x.ReceivedAt).IsDescending()
            .HasFilter("[MatchResult] <> N'Matched' AND [Resolution] IS NULL")
            .HasDatabaseName("IX_PaymentTransactions_NeedsReview");
        builder.HasIndex(x => x.ResolvedByUserId).HasDatabaseName("IX_PaymentTransactions_ResolvedByUserId");
    }
}
