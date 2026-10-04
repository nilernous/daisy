namespace Daisy.Domain.Entities;

/// <summary>One wishlist entry; a product appears at most once per user (BR-18).</summary>
public class Wishlist
{
    public int UserId { get; set; }

    public int ProductId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    public Product? Product { get; set; }
}
