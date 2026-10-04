using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.ViewComponents;

/// <summary>Category navigation in the masthead (UC-13).</summary>
public sealed class CategoryMenuViewComponent(ProductApiService products, ILogger<CategoryMenuViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            return View(await products.GetCategoriesAsync());
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            logger.LogWarning(ex, "Could not load the category menu.");
            return View(Array.Empty<CategoryNode>());
        }
    }
}

/// <summary>Cart count in the masthead (UC-27).</summary>
public sealed class CartBadgeViewComponent(CartApiService cart) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = 0;
        try
        {
            count = (await cart.GetAsync()).TotalQuantity;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            // The badge is not worth failing the page for.
        }

        return View(count);
    }
}
