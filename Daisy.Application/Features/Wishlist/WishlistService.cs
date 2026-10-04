using Daisy.Application.Common;
using Daisy.Application.Features.Products;
using Daisy.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Wishlist;

public sealed class WishlistService
{
    private readonly IApplicationDbContext _db;
    private readonly CatalogVisibility _visibility;
    private readonly TimeProvider _time;

    public WishlistService(IApplicationDbContext db, CatalogVisibility visibility, TimeProvider time)
    {
        _db = db;
        _visibility = visibility;
        _time = time;
    }

    /// <summary>UC-25. Newest first; hidden products are returned as unavailable.</summary>
    public async Task<IReadOnlyList<WishlistItemDto>> GetAsync(int userId, CancellationToken ct)
    {
        var now = _time.GetVietnamNow();
        var visible = await _visibility.GetVisibleCategoryIdsAsync(ct);
        var entries = await _db.Wishlists.AsNoTracking()
            .Where(w => w.UserId == userId)
            .Include(w => w.Product!).ThenInclude(p => p.Images)
            .Include(w => w.Product!).ThenInclude(p => p.Variants)
            .OrderByDescending(w => w.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);

        return entries.Select(w =>
        {
            var p = w.Product!;
            var available = p.IsActive && visible.Contains(p.CategoryId) && p.Variants.Any(v => v.IsActive);
            return new WishlistItemDto(
                p.ProductId, p.Slug, p.ProductName, ProductImageSelector.Pick(p.Images),
                p.BasePrice, p.GetEffectivePrice(now), p.IsOnSale(now), available, w.CreatedAt);
        }).ToList();
    }

    /// <summary>UC-23. Adding a product that is already saved is not an error (BR-18).</summary>
    public async Task AddAsync(int userId, int productId, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null || !await _visibility.IsVisibleAsync(product, null, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        if (await _db.Wishlists.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct))
        {
            return;
        }

        _db.Wishlists.Add(new Domain.Entities.Wishlist
        {
            UserId = userId,
            ProductId = productId,
            CreatedAt = _time.GetVietnamNow(),
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-24. Idempotent.</summary>
    public Task RemoveAsync(int userId, int productId, CancellationToken ct) =>
        _db.Wishlists.Where(w => w.UserId == userId && w.ProductId == productId).ExecuteDeleteAsync(ct);
}
