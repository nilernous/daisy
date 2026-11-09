using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>Home page collections (UC-19 – UC-22).</summary>
public sealed class HomeController(ProductApiService products) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        const string fields = "id,name,slug,brand,categoryId,basePrice,salePrice,effectivePrice,isOnSale,saleEndAt,thumbnailUrl,averageRating,reviewCount,soldCount,isFeatured,createdAt,variants";
        // Sequential on purpose: the API handler reads HttpContext, which is not thread-safe.
        var sale = (await products.QueryAsync(new ODataQuery().Filter("isOnSale eq true and saleEndAt ne null").OrderBy("saleEndAt").Top(4).Select(fields))).Value;
        var featured = (await products.QueryAsync(new ODataQuery().Filter("isFeatured eq true").OrderBy("createdAt desc").Top(8).Select(fields))).Value;
        var newest = (await products.QueryAsync(new ODataQuery().OrderBy("createdAt desc").Top(8).Select(fields))).Value;
        var best = (await products.QueryAsync(new ODataQuery().OrderBy("soldCount desc").Top(8).Select(fields))).Value;
        var categories = await products.GetCategoriesAsync();

        var endsAt = sale.Select(p => p.SaleEndAt).Min();
        return View(new HomeViewModel(sale, endsAt, featured, newest, best, categories));
    }

    // Re-executed by UseStatusCodePagesWithReExecute / UseExceptionHandler with the original method,
    // so a failed POST (e.g. an expired antiforgery token) must reach these too instead of ending in 405.
    [Route("home/status/{code:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Status(int code) =>
        code == 404 ? View("NotFound") : View("Error");

    [Route("home/error")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error() => View("Error");
}
