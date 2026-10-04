namespace Daisy.Domain.Entities;

public class Category
{
    public int CategoryId { get; set; }

    public int? ParentCategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public Category? ParentCategory { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
