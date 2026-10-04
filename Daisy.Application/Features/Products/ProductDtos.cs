using Daisy.Application.Common;

namespace Daisy.Application.Features.Products;

/// <summary>Size / color pair used to filter products with OData any() (UC-17).</summary>
public sealed class VariantOptionDto
{
    public string? Size { get; init; }

    public string? Color { get; init; }
}

/// <summary>Product list item; also the OData entity of /odata/products (API_DESIGN §5.2, §7.1).</summary>
public sealed class ProductListDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Brand { get; init; }

    public int CategoryId { get; init; }

    public string? GenderTarget { get; init; }

    public decimal BasePrice { get; init; }

    public decimal? SalePrice { get; init; }

    public decimal EffectivePrice { get; init; }

    public bool IsOnSale { get; init; }

    public DateTime? SaleEndAt { get; init; }

    public string? ThumbnailUrl { get; init; }

    public decimal AverageRating { get; init; }

    public int ReviewCount { get; init; }

    public int SoldCount { get; init; }

    public bool IsFeatured { get; init; }

    public DateTime CreatedAt { get; init; }

    public List<VariantOptionDto> Variants { get; init; } = [];
}

public sealed record CategoryRefDto(int Id, string Name, string Slug);

public sealed record ProductVariantDto(
    int Id,
    string Sku,
    string? Size,
    string? Color,
    string? ColorHex,
    decimal ExtraPrice,
    decimal Price,
    int StockQuantity,
    bool InStock,
    bool IsLowStock);

public sealed record ProductImageDto(
    int Id,
    string Url,
    bool IsThumbnail,
    int SortOrder,
    int? VariantId,
    string? Color);

public sealed record ProductDetailDto(
    int Id,
    string Slug,
    string Name,
    string? Brand,
    string? Description,
    string? Material,
    string? GenderTarget,
    CategoryRefDto Category,
    decimal BasePrice,
    decimal? SalePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    DateTime? SaleEndAt,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images);

// ---- Admin -------------------------------------------------------------------------

public sealed record AdminProductQuery : PageRequest
{
    public string? Search { get; init; }

    public int? CategoryId { get; init; }

    public bool? IsActive { get; init; }

    public bool? IsFeatured { get; init; }

    public bool? IsOnSale { get; init; }

    public bool? LowStock { get; init; }
}

public sealed record ProductAdminListDto(
    int Id,
    string Name,
    string Slug,
    string? Brand,
    int CategoryId,
    string CategoryName,
    decimal BasePrice,
    decimal? SalePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    string? ThumbnailUrl,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    bool IsFeatured,
    bool IsActive,
    int VariantCount,
    int TotalStock,
    bool HasLowStock,
    DateTime CreatedAt);

public sealed record VariantAdminDto(
    int Id,
    int ProductId,
    string Sku,
    string? Size,
    string? Color,
    string? ColorHex,
    decimal ExtraPrice,
    int StockQuantity,
    int LowStockThreshold,
    bool IsActive);

public sealed record ProductAdminDto(
    int Id,
    int CategoryId,
    string Name,
    string Slug,
    string? Brand,
    string? Description,
    string? Material,
    string? GenderTarget,
    decimal BasePrice,
    decimal? SalePrice,
    DateTime? SaleStartAt,
    DateTime? SaleEndAt,
    bool IsOnSale,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    bool IsFeatured,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<VariantAdminDto> Variants,
    IReadOnlyList<ProductImageDto> Images);

public sealed record ProductRequest(
    int CategoryId,
    string Name,
    string Slug,
    string? Brand,
    string? Description,
    string? Material,
    string? GenderTarget,
    decimal BasePrice);

public sealed record SaleRequest(decimal SalePrice, DateTime? StartAt, DateTime? EndAt);

public sealed record VariantRequest(
    string Sku,
    string? Size,
    string? Color,
    string? ColorHex,
    decimal ExtraPrice,
    int? StockQuantity,
    int LowStockThreshold = 5);

public sealed record ImageOrderRequest(IReadOnlyList<int> ImageIds);

public sealed record ImageVariantRequest(int? VariantId);
