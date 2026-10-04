namespace Daisy.Domain.Entities;

public class ProductVariant
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string? Size { get; set; }

    public string? Color { get; set; }

    public string? ColorHex { get; set; }

    public decimal ExtraPrice { get; set; }

    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public Product? Product { get; set; }

    public bool InStock => StockQuantity > 0;

    /// <summary>Low stock when StockQuantity &lt;= LowStockThreshold (BR-17).</summary>
    public bool IsLowStock => StockQuantity <= LowStockThreshold;

    /// <summary>Product effective price plus the variant extra price (BR-14). Requires <see cref="Product"/>.</summary>
    public decimal GetPrice(DateTime now) =>
        (Product ?? throw new InvalidOperationException("Product is not loaded.")).GetEffectivePrice(now) + ExtraPrice;

    /// <summary>Available for the storefront: the variant and its product are active (BR-10).</summary>
    public bool IsAvailable => IsActive && Product is { IsActive: true };
}
