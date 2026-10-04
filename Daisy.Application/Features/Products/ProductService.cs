using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Products;

/// <summary>Storefront catalog (UC-14 – UC-22) and admin product management (UC-45, UC-46, UC-49, UC-50).</summary>
public sealed class ProductService
{
    private readonly IApplicationDbContext _db;
    private readonly CatalogVisibility _visibility;
    private readonly TimeProvider _time;
    private readonly IValidator<ProductRequest> _productValidator;
    private readonly IValidator<SaleRequest> _saleValidator;

    public ProductService(
        IApplicationDbContext db,
        CatalogVisibility visibility,
        TimeProvider time,
        IValidator<ProductRequest> productValidator,
        IValidator<SaleRequest> saleValidator)
    {
        _db = db;
        _visibility = visibility;
        _time = time;
        _productValidator = productValidator;
        _saleValidator = saleValidator;
    }

    /// <summary>
    /// Query behind GET /odata/products: active products in visible categories with at least one
    /// active variant (BR-10); EffectivePrice and IsOnSale use the request time (BR-14).
    /// </summary>
    public async Task<IQueryable<ProductListDto>> QueryStorefrontAsync(CancellationToken ct)
    {
        var now = _time.GetVietnamNow();
        var visible = (await _visibility.GetVisibleCategoryIdsAsync(ct)).ToList();

        return _db.Products.AsNoTracking()
            .Where(p => p.IsActive && visible.Contains(p.CategoryId) && p.Variants.Any(v => v.IsActive))
            .Select(p => new ProductListDto
            {
                Id = p.ProductId,
                Name = p.ProductName,
                Slug = p.Slug,
                Brand = p.Brand,
                CategoryId = p.CategoryId,
                GenderTarget = p.GenderTarget,
                BasePrice = p.BasePrice,
                SalePrice = p.SalePrice,
                IsOnSale = p.SalePrice != null
                    && (p.SaleStartAt == null || p.SaleStartAt <= now)
                    && (p.SaleEndAt == null || p.SaleEndAt >= now),
                EffectivePrice = p.SalePrice != null
                    && (p.SaleStartAt == null || p.SaleStartAt <= now)
                    && (p.SaleEndAt == null || p.SaleEndAt >= now)
                        ? p.SalePrice.Value
                        : p.BasePrice,
                SaleEndAt = p.SaleEndAt,
                ThumbnailUrl = p.Images
                    .OrderByDescending(i => i.IsThumbnail)
                    .ThenBy(i => i.SortOrder)
                    .ThenBy(i => i.ImageId)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                AverageRating = p.AverageRating,
                ReviewCount = p.ReviewCount,
                SoldCount = p.SoldCount,
                IsFeatured = p.IsFeatured,
                CreatedAt = p.CreatedAt,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .Select(v => new VariantOptionDto { Size = v.Size, Color = v.Color })
                    .ToList(),
            });
    }

    /// <summary>UC-15.</summary>
    public async Task<ProductDetailDto> GetDetailAsync(string slug, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

        if (product is null || !await _visibility.IsVisibleAsync(product, null, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var now = _time.GetVietnamNow();
        var effective = product.GetEffectivePrice(now);
        var variants = product.Variants.Where(v => v.IsActive).OrderBy(v => v.VariantId).ToList();
        var colorByVariant = product.Variants.ToDictionary(v => v.VariantId, v => v.Color);

        return new ProductDetailDto(
            product.ProductId,
            product.Slug,
            product.ProductName,
            product.Brand,
            product.Description,
            product.Material,
            product.GenderTarget,
            new CategoryRefDto(product.Category!.CategoryId, product.Category.CategoryName, product.Category.Slug),
            product.BasePrice,
            product.SalePrice,
            effective,
            product.IsOnSale(now),
            product.SaleEndAt,
            product.AverageRating,
            product.ReviewCount,
            product.SoldCount,
            variants.Select(v => new ProductVariantDto(
                v.VariantId, v.Sku, v.Size, v.Color, v.ColorHex, v.ExtraPrice, effective + v.ExtraPrice,
                v.StockQuantity, v.InStock, v.IsLowStock)).ToList(),
            product.Images
                .Where(i => i.VariantId is null || variants.Any(v => v.VariantId == i.VariantId))
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ImageId)
                .Select(i => ToImageDto(i, colorByVariant))
                .ToList());
    }

    /// <summary>Admin list including hidden products.</summary>
    public async Task<PagedResult<ProductAdminListDto>> GetAdminListAsync(AdminProductQuery query, CancellationToken ct)
    {
        query.ValidatePage();
        var now = _time.GetVietnamNow();
        var products = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p =>
                p.ProductName.Contains(term)
                || (p.Brand != null && p.Brand.Contains(term))
                || p.Variants.Any(v => v.Sku.Contains(term)));
        }

        if (query.CategoryId is int categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (query.IsActive is bool isActive)
        {
            products = products.Where(p => p.IsActive == isActive);
        }

        if (query.IsFeatured is bool isFeatured)
        {
            products = products.Where(p => p.IsFeatured == isFeatured);
        }

        if (query.IsOnSale is bool isOnSale)
        {
            products = products.Where(p => (p.SalePrice != null
                && (p.SaleStartAt == null || p.SaleStartAt <= now)
                && (p.SaleEndAt == null || p.SaleEndAt >= now)) == isOnSale);
        }

        if (query.LowStock is true)
        {
            products = products.Where(p => p.Variants.Any(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold));
        }

        var page = await products
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
            .Select(p => new
            {
                p.ProductId,
                p.ProductName,
                p.Slug,
                p.Brand,
                p.CategoryId,
                CategoryName = p.Category!.CategoryName,
                p.BasePrice,
                p.SalePrice,
                IsOnSale = p.SalePrice != null
                    && (p.SaleStartAt == null || p.SaleStartAt <= now)
                    && (p.SaleEndAt == null || p.SaleEndAt >= now),
                Thumbnail = p.Images
                    .OrderByDescending(i => i.IsThumbnail).ThenBy(i => i.SortOrder).ThenBy(i => i.ImageId)
                    .Select(i => i.ImageUrl).FirstOrDefault(),
                p.AverageRating,
                p.ReviewCount,
                p.SoldCount,
                p.IsFeatured,
                p.IsActive,
                VariantCount = p.Variants.Count,
                TotalStock = p.Variants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0,
                HasLowStock = p.Variants.Any(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold),
                p.CreatedAt,
            })
            .ToPagedResultAsync(query, ct);

        return page.Map(p => new ProductAdminListDto(
            p.ProductId, p.ProductName, p.Slug, p.Brand, p.CategoryId, p.CategoryName, p.BasePrice, p.SalePrice,
            p.IsOnSale ? p.SalePrice!.Value : p.BasePrice, p.IsOnSale, p.Thumbnail, p.AverageRating, p.ReviewCount,
            p.SoldCount, p.IsFeatured, p.IsActive, p.VariantCount, p.TotalStock, p.HasLowStock, p.CreatedAt));
    }

    public async Task<ProductAdminDto> GetAdminAsync(int id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ProductId == id, ct)
            ?? throw new NotFoundException("Product not found.");
        return ToAdminDto(product, _time.GetVietnamNow());
    }

    /// <summary>UC-45.</summary>
    public async Task<ProductAdminDto> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        await _productValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        await EnsureSlugFreeAsync(request.Slug, null, ct);

        var now = _time.GetVietnamNow();
        var product = new Product { IsActive = true, CreatedAt = now };
        Apply(product, request);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return await GetAdminAsync(product.ProductId, ct);
    }

    /// <summary>UC-45. Existing orders keep their snapshots (BR-28).</summary>
    public async Task<ProductAdminDto> UpdateAsync(int id, ProductRequest request, CancellationToken ct)
    {
        await _productValidator.ValidateAndThrowAsync(request, ct);
        var product = await FindAsync(id, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        await EnsureSlugFreeAsync(request.Slug, id, ct);

        Apply(product, request);
        product.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
        return await GetAdminAsync(id, ct);
    }

    /// <summary>UC-46. Hide instead of delete (BR-11).</summary>
    public async Task SetVisibilityAsync(int id, bool isActive, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.IsActive = isActive;
        product.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-49.</summary>
    public async Task SetFeaturedAsync(int id, bool isFeatured, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.IsFeatured = isFeatured;
        product.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-50 (Admin).</summary>
    public async Task SetSaleAsync(int id, SaleRequest request, CancellationToken ct)
    {
        await _saleValidator.ValidateAndThrowAsync(request, ct);
        var product = await FindAsync(id, ct);
        product.SetSale(request.SalePrice, request.StartAt, request.EndAt);
        product.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-50 (Admin).</summary>
    public async Task ClearSaleAsync(int id, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.ClearSale();
        product.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
    }

    internal static ProductImageDto ToImageDto(ProductImage image, IReadOnlyDictionary<int, string?> colorByVariant) =>
        new(
            image.ImageId,
            image.ImageUrl,
            image.IsThumbnail,
            image.SortOrder,
            image.VariantId,
            image.VariantId is int vid ? colorByVariant.GetValueOrDefault(vid) : null);

    internal static VariantAdminDto ToVariantDto(ProductVariant v) =>
        new(v.VariantId, v.ProductId, v.Sku, v.Size, v.Color, v.ColorHex, v.ExtraPrice, v.StockQuantity, v.LowStockThreshold, v.IsActive);

    private static ProductAdminDto ToAdminDto(Product p, DateTime now)
    {
        var colorByVariant = p.Variants.ToDictionary(v => v.VariantId, v => v.Color);
        return new ProductAdminDto(
            p.ProductId, p.CategoryId, p.ProductName, p.Slug, p.Brand, p.Description, p.Material, p.GenderTarget,
            p.BasePrice, p.SalePrice, p.SaleStartAt, p.SaleEndAt, p.IsOnSale(now), p.AverageRating, p.ReviewCount,
            p.SoldCount, p.IsFeatured, p.IsActive, p.CreatedAt, p.UpdatedAt,
            p.Variants.OrderBy(v => v.VariantId).Select(ToVariantDto).ToList(),
            p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.ImageId).Select(i => ToImageDto(i, colorByVariant)).ToList());
    }

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id, ct)
        ?? throw new NotFoundException("Product not found.");

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId, ct))
        {
            throw new NotFoundException("Category not found.");
        }
    }

    private async Task EnsureSlugFreeAsync(string slug, int? exceptId, CancellationToken ct)
    {
        if (await _db.Products.AnyAsync(p => p.Slug == slug && p.ProductId != exceptId, ct))
        {
            throw new ConflictException(ErrorCodes.SlugAlreadyExists, "Another product already uses this slug.");
        }
    }

    private static void Apply(Product product, ProductRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.ProductName = request.Name.Trim();
        product.Slug = request.Slug.Trim();
        product.Brand = ValidationRules.TrimToNull(request.Brand);
        product.Description = ValidationRules.TrimToNull(request.Description);
        product.Material = ValidationRules.TrimToNull(request.Material);
        product.GenderTarget = request.GenderTarget;
        product.SetBasePrice(request.BasePrice);
    }
}
