using Daisy.Domain.Entities;

namespace Daisy.Application.Features.Categories;

public sealed record CategoryTreeDto(
    int Id,
    string Name,
    string Slug,
    string? ImageUrl,
    int SortOrder,
    IReadOnlyList<CategoryTreeDto> Children);

public sealed record CategoryAdminTreeDto(
    int Id,
    string Name,
    string Slug,
    int? ParentCategoryId,
    string? ImageUrl,
    int SortOrder,
    bool IsActive,
    int ProductCount,
    IReadOnlyList<CategoryAdminTreeDto> Children);

public sealed record CategoryDto(
    int Id,
    string Name,
    string Slug,
    int? ParentCategoryId,
    string? ImageUrl,
    int SortOrder,
    bool IsActive)
{
    public static CategoryDto From(Category c) =>
        new(c.CategoryId, c.CategoryName, c.Slug, c.ParentCategoryId, c.ImageUrl, c.SortOrder, c.IsActive);
}

public sealed record CategoryRequest(
    string Name,
    string Slug,
    int? ParentCategoryId,
    string? ImageUrl,
    int SortOrder);
