using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>Listing, search, filter and sort through OData (UC-14, UC-16 – UC-18), product detail (UC-15).</summary>
[Route("products")]
public sealed class ProductsController(ProductApiService products, CartApiService cart, ProfileApiService profile) : WebController
{
    private const int PageSize = 24;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ProductFilter filter)
    {
        var categories = await products.GetCategoriesAsync();
        var category = Find(categories, filter.Category);
        var query = new ODataQuery().Page(filter.Page, PageSize);

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = ODataQuery.Literal(filter.Q.Trim().ToLowerInvariant());
            query.Filter($"contains(tolower(name),{term}) or contains(tolower(brand),{term})");
        }

        if (category is not null)
        {
            // A parent category includes its subcategories.
            query.Filter($"categoryId in ({string.Join(',', category.SelfAndDescendantIds())})");
        }

        if (filter.Sale)
        {
            query.Filter("isOnSale eq true");
        }

        if (filter.Min is decimal min)
        {
            query.Filter($"effectivePrice ge {min:0}");
        }

        if (filter.Max is decimal max)
        {
            query.Filter($"effectivePrice le {max:0}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Gender))
        {
            query.Filter($"genderTarget eq {ODataQuery.Literal(filter.Gender)}");
        }

        var variantConditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Size))
        {
            variantConditions.Add($"v/size eq {ODataQuery.Literal(filter.Size)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Color))
        {
            variantConditions.Add($"v/color eq {ODataQuery.Literal(filter.Color)}");
        }

        if (variantConditions.Count > 0)
        {
            query.Filter($"variants/any(v: {string.Join(" and ", variantConditions)})");
        }

        query.OrderBy(filter.Sort switch
        {
            "price-asc" => "effectivePrice asc",
            "price-desc" => "effectivePrice desc",
            "best" => "soldCount desc",
            "rating" => "averageRating desc",
            _ => "createdAt desc",
        });

        var page = await products.QueryAsync(query);
        var total = page.Count ?? page.Value.Count;
        var heading = !string.IsNullOrWhiteSpace(filter.Q) ? $"Kết quả cho “{filter.Q.Trim()}”"
            : category is not null ? category.Name
            : filter.Sale ? "Đang giảm giá"
            : "Tất cả sản phẩm";

        return View(new ProductListViewModel(filter, page.Value, total, (int)Math.Ceiling(total / (double)PageSize), heading, category, categories));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, int reviewPage = 1, int? rating = null)
    {
        var product = await products.GetDetailAsync(slug);
        var reviews = await products.GetReviewsAsync(product.Id, Math.Max(1, reviewPage), rating is >= 1 and <= 5 ? rating : null);
        ViewData["Title"] = product.Name;
        ViewData["Description"] = product.Description;
        return View(new ProductDetailViewModel(product, reviews, rating, User.IsInRole("Customer")));
    }

    /// <summary>UC-26. Guests use their session cart.</summary>
    [HttpPost("{slug}/cart")]
    public async Task<IActionResult> AddToCart(string slug, int variantId, int quantity = 1, bool buyNow = false)
    {
        if (variantId <= 0)
        {
            Flash("Hãy chọn size và màu trước khi thêm vào giỏ.", "bad");
            return Redirect($"/products/{slug}");
        }

        try
        {
            await cart.AddAsync(variantId, Math.Max(1, quantity));
            if (buyNow)
            {
                return Redirect("/cart");
            }

            Flash("Đã thêm vào giỏ hàng.");
        }
        catch (ApiException ex) when (ex.Status is not System.Net.HttpStatusCode.Unauthorized)
        {
            Flash(ErrorMessages.For(ex), "bad");
        }

        return Redirect($"/products/{slug}");
    }

    /// <summary>UC-23.</summary>
    [HttpPost("{slug}/wishlist")]
    public async Task<IActionResult> AddToWishlist(string slug, int productId)
    {
        if (!User.IsInRole("Customer"))
        {
            return RedirectToAction("Login", "Account", new { returnUrl = $"/products/{slug}" });
        }

        await profile.AddToWishlistAsync(productId);
        Flash("Đã lưu vào danh sách yêu thích.");
        return Redirect($"/products/{slug}");
    }

    private static CategoryNode? Find(IEnumerable<CategoryNode> nodes, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (node.Slug == slug)
            {
                return node;
            }

            if (Find(node.Children, slug) is { } child)
            {
                return child;
            }
        }

        return null;
    }
}
