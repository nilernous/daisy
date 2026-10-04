using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Daisy.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<UserAddress> UserAddresses { get; }

    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductVariant> ProductVariants { get; }

    DbSet<ProductImage> ProductImages { get; }

    DbSet<CartItem> CartItems { get; }

    DbSet<Wishlist> Wishlists { get; }

    DbSet<Voucher> Vouchers { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<PaymentTransaction> PaymentTransactions { get; }

    DbSet<Review> Reviews { get; }

    /// <summary>Used for explicit transactions (ARCHITECTURE §27).</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
