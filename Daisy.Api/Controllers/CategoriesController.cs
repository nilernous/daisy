using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-13.</summary>
[Route("api/categories")]
[AllowAnonymous]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly CategoryService _categories;

    public CategoriesController(CategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryTreeDto>>> GetCategoryTree(CancellationToken ct) =>
        Ok(await _categories.GetTreeAsync(ct));
}
