namespace Daisy.Domain.Entities;

/// <summary>An order line; product details are snapshots taken at checkout (BR-28).</summary>
public class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }

    public int VariantId { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;

    public string? SizeSnapshot { get; set; }

    public string? ColorSnapshot { get; set; }

    public string? ImageUrlSnapshot { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary>Computed column: UnitPrice * Quantity.</summary>
    public decimal LineTotal { get; private set; }

    public Order? Order { get; set; }

    public ProductVariant? Variant { get; set; }

    public Review? Review { get; set; }
}
