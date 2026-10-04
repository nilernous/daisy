using Daisy.Application.Interfaces;
using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the schema in infra/database/DaisyDB.sql (13 tables).
/// One configuration class per table maps the existing names, constraints and indexes.
/// </summary>
public sealed class DaisyDbContext : DbContext, IApplicationDbContext
{
    public DaisyDbContext(DbContextOptions<DaisyDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Wishlist> Wishlists => Set<Wishlist>();

    public DbSet<Voucher> Vouchers => Set<Voucher>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DaisyDbContext).Assembly);
    }
}
