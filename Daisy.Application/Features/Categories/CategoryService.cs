using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Categories;

public sealed class CategoryService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CategoryRequest> _validator;

    public CategoryService(IApplicationDbContext db, IValidator<CategoryRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    /// <summary>UC-13. Active categories only; a hidden parent hides its children (BR-10, BR-13).</summary>
    public async Task<IReadOnlyList<CategoryTreeDto>> GetTreeAsync(CancellationToken ct)
    {
        var all = await _db.Categories.AsNoTracking().ToListAsync(ct);
        return Build(null);

        IReadOnlyList<CategoryTreeDto> Build(int? parentId) => all
            .Where(c => c.ParentCategoryId == parentId && c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CategoryName)
            .Select(c => new CategoryTreeDto(c.CategoryId, c.CategoryName, c.Slug, c.ImageUrl, c.SortOrder, Build(c.CategoryId)))
            .ToList();
    }

    /// <summary>Admin tree including hidden categories (UC-43).</summary>
    public async Task<IReadOnlyList<CategoryAdminTreeDto>> GetAdminTreeAsync(CancellationToken ct)
    {
        var all = await _db.Categories.AsNoTracking().ToListAsync(ct);
        var counts = await _db.Products
            .GroupBy(p => p.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return Build(null);

        IReadOnlyList<CategoryAdminTreeDto> Build(int? parentId) => all
            .Where(c => c.ParentCategoryId == parentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CategoryName)
            .Select(c => new CategoryAdminTreeDto(
                c.CategoryId,
                c.CategoryName,
                c.Slug,
                c.ParentCategoryId,
                c.ImageUrl,
                c.SortOrder,
                c.IsActive,
                counts.GetValueOrDefault(c.CategoryId),
                Build(c.CategoryId)))
            .ToList();
    }

    public async Task<CategoryDto> GetAsync(int id, CancellationToken ct)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == id, ct)
            ?? throw new NotFoundException("Category not found.");
        return CategoryDto.From(category);
    }

    /// <summary>UC-43.</summary>
    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        await EnsureSlugFreeAsync(request.Slug, null, ct);
        await EnsureParentValidAsync(null, request.ParentCategoryId, ct);

        var category = new Category { IsActive = true };
        Apply(category, request);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return CategoryDto.From(category);
    }

    /// <summary>UC-43.</summary>
    public async Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id, ct)
            ?? throw new NotFoundException("Category not found.");
        await EnsureSlugFreeAsync(request.Slug, id, ct);
        await EnsureParentValidAsync(id, request.ParentCategoryId, ct);

        Apply(category, request);
        await _db.SaveChangesAsync(ct);
        return CategoryDto.From(category);
    }

    /// <summary>UC-44. Hide instead of delete (BR-11).</summary>
    public async Task SetVisibilityAsync(int id, bool isActive, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id, ct)
            ?? throw new NotFoundException("Category not found.");
        category.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureSlugFreeAsync(string slug, int? exceptId, CancellationToken ct)
    {
        if (await _db.Categories.AnyAsync(c => c.Slug == slug && c.CategoryId != exceptId, ct))
        {
            throw new ConflictException(ErrorCodes.SlugAlreadyExists, "Another category already uses this slug.");
        }
    }

    /// <summary>A category cannot be its own parent or a descendant of itself (BR-13).</summary>
    private async Task EnsureParentValidAsync(int? categoryId, int? parentId, CancellationToken ct)
    {
        if (parentId is null)
        {
            return;
        }

        var parents = await _db.Categories.AsNoTracking()
            .Select(c => new { c.CategoryId, c.ParentCategoryId })
            .ToDictionaryAsync(c => c.CategoryId, c => c.ParentCategoryId, ct);

        if (!parents.ContainsKey(parentId.Value))
        {
            throw new NotFoundException("Parent category not found.");
        }

        int? current = parentId;
        var guard = 0;
        while (current is not null && guard++ < 64)
        {
            if (current == categoryId)
            {
                throw new BadRequestException(ErrorCodes.InvalidCategoryParent, "A category cannot be placed under itself or one of its subcategories.");
            }

            current = parents.GetValueOrDefault(current.Value);
        }
    }

    private static void Apply(Category category, CategoryRequest request)
    {
        category.CategoryName = request.Name.Trim();
        category.Slug = request.Slug.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.ImageUrl = ValidationRules.TrimToNull(request.ImageUrl);
        category.SortOrder = request.SortOrder;
    }
}
