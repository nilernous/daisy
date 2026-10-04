namespace Daisy.Domain.Entities;

/// <summary>
/// A cart line. It belongs to a user or to a guest session (BR-19); the application always sets exactly one.
/// </summary>
public class CartItem
{
    public int CartItemId { get; set; }

    public int? UserId { get; set; }

    public string? SessionId { get; set; }

    public int VariantId { get; set; }

    public int Quantity { get; set; }

    public bool IsSelected { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public ProductVariant? Variant { get; set; }
}
