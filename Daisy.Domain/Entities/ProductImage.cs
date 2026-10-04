namespace Daisy.Domain.Entities;

public class ProductImage
{
    public int ImageId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Links the image to a variant so the gallery can switch by color (BR-16).</summary>
    public int? VariantId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsThumbnail { get; set; }

    public int SortOrder { get; set; }

    public Product? Product { get; set; }

    public ProductVariant? Variant { get; set; }
}
