using Daisy.Application.Interfaces;
using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Products;

/// <summary>
/// Storefront visibility (BR-10): a category is visible when it and all its ancestors are active.
/// Categories are few, so the visible set is computed in memory once per request.
/// </summary>
public sealed class CatalogVisibility
{
    private readonly IApplicationDbContext _db;
    private HashSet<int>? _visibleCategoryIds;

    public CatalogVisibility(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<int>> GetVisibleCategoryIdsAsync(CancellationToken ct)
    {
        if (_visibleCategoryIds is not null)
        {
            return _visibleCategoryIds;
        }

        var categories = await _db.Categories
            .AsNoTracking()
            .Select(c => new { c.CategoryId, c.ParentCategoryId, c.IsActive })
            .ToListAsync(ct);

        var byId = categories.ToDictionary(c => c.CategoryId);
        var visible = new HashSet<int>();
        foreach (var category in categories)
        {
            var current = category;
            var ok = true;
            var guard = 0;
            while (current is not null && guard++ < 64)
            {
                if (!current.IsActive)
                {
                    ok = false;
                    break;
                }

                current = current.ParentCategoryId is int parentId && byId.TryGetValue(parentId, out var parent)
                    ? parent
                    : null;
            }

            if (ok)
            {
                visible.Add(category.CategoryId);
            }
        }

        _visibleCategoryIds = visible;
        return visible;
    }

    /// <summary>True when the product, its variant (if any) and its category are visible.</summary>
    public async Task<bool> IsVisibleAsync(Product product, ProductVariant? variant, CancellationToken ct)
    {
        if (!product.IsActive || variant is { IsActive: false })
        {
            return false;
        }

        var visible = await GetVisibleCategoryIdsAsync(ct);
        return visible.Contains(product.CategoryId);
    }
}

public static class ProductImageSelector
{
    /// <summary>
    /// The image shown for a product or variant: an image linked to the variant first,
    /// then the thumbnail, then the first image (BR-16).
    /// </summary>
    public static string? Pick(IEnumerable<ProductImage> images, int? variantId = null)
    {
        var ordered = images.OrderBy(i => i.SortOrder).ThenBy(i => i.ImageId).ToList();
        if (variantId is not null)
        {
            var forVariant = ordered.FirstOrDefault(i => i.VariantId == variantId);
            if (forVariant is not null)
            {
                return forVariant.ImageUrl;
            }
        }

        return (ordered.FirstOrDefault(i => i.IsThumbnail) ?? ordered.FirstOrDefault())?.ImageUrl;
    }
}
