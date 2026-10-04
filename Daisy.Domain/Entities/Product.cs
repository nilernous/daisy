using Daisy.Domain.Common;

namespace Daisy.Domain.Entities;

public class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? Description { get; set; }

    public string? Material { get; set; }

    public string? GenderTarget { get; set; }

    public decimal BasePrice { get; private set; }

    public decimal? SalePrice { get; private set; }

    public DateTime? SaleStartAt { get; private set; }

    public DateTime? SaleEndAt { get; private set; }

    public decimal AverageRating { get; set; }

    public int ReviewCount { get; set; }

    public int SoldCount { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    /// <summary>A sale is active when SalePrice is set and now is within the sale period (BR-14).</summary>
    public bool IsOnSale(DateTime now) =>
        SalePrice is not null
        && (SaleStartAt is null || SaleStartAt <= now)
        && (SaleEndAt is null || now <= SaleEndAt);

    public decimal GetEffectivePrice(DateTime now) => IsOnSale(now) ? SalePrice!.Value : BasePrice;

    /// <summary>BasePrice cannot be negative or below an existing sale price (BR-14).</summary>
    public void SetBasePrice(decimal basePrice)
    {
        if (basePrice < 0)
        {
            throw new DomainException(ErrorCodes.InvalidSale, "Base price cannot be negative.");
        }

        if (SalePrice is not null && SalePrice > basePrice)
        {
            throw new DomainException(ErrorCodes.InvalidSale, "Base price cannot be lower than the current sale price.");
        }

        BasePrice = basePrice;
    }

    public void SetSale(decimal salePrice, DateTime? startAt, DateTime? endAt)
    {
        if (salePrice < 0 || salePrice > BasePrice)
        {
            throw new DomainException(ErrorCodes.InvalidSale, "Sale price must be between 0 and the base price.");
        }

        if (startAt is not null && endAt is not null && startAt >= endAt)
        {
            throw new DomainException(ErrorCodes.InvalidSale, "Sale start must be before sale end.");
        }

        SalePrice = salePrice;
        SaleStartAt = startAt;
        SaleEndAt = endAt;
    }

    public void ClearSale()
    {
        SalePrice = null;
        SaleStartAt = null;
        SaleEndAt = null;
    }
}
