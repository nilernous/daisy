namespace Daisy.Domain.Entities;

/// <summary>A rating for a purchased order item (BR-37); hidden reviews are kept (BR-39).</summary>
public class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }

    public int ProductId { get; set; }

    public int OrderItemId { get; set; }

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    public Product? Product { get; set; }

    public OrderItem? OrderItem { get; set; }
}
