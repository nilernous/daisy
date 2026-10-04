using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-43, UC-44.</summary>
[Route("api/admin/categories")]
[Authorize(Policy = Policies.Staff)]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly CategoryService _categories;

    public CategoriesController(CategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryAdminTreeDto>>> GetAdminCategoryTree(CancellationToken ct) =>
        Ok(await _categories.GetAdminTreeAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken ct) =>
        await _categories.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> CreateCategory(CategoryRequest request, CancellationToken ct)
    {
        var category = await _categories.CreateAsync(request, ct);
        return Created($"/api/admin/categories/{category.Id}", category);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryRequest request, CancellationToken ct) =>
        await _categories.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/visibility")]
    public async Task<IActionResult> SetCategoryVisibility(int id, SetActiveRequest request, CancellationToken ct)
    {
        await _categories.SetVisibilityAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
