using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-61, UC-62.</summary>
public sealed class ReviewsController(AdminApiService admin) : AdminController
{
    private const int PageSize = 20;

    public async Task<IActionResult> Index(int? rating, bool? visible, string? product, int page = 1)
    {
        var query = new ODataQuery().OrderBy("createdAt desc").Page(page, PageSize);
        if (rating is >= 1 and <= 5) query.Filter($"rating eq {rating}");
        if (visible is not null) query.Filter($"isVisible eq {visible.ToString()!.ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(product)) query.Filter($"contains(tolower(productName),{ODataQuery.Literal(product.Trim().ToLowerInvariant())})");

        var result = await admin.QueryReviewsAsync(query);
        var total = result.Count ?? result.Value.Count;
        ViewData["Title"] = "Đánh giá";
        return View(new ReviewsAdminViewModel(result.Value, total, page, (int)Math.Ceiling(total / (double)PageSize), rating, visible, product));
    }

    [HttpPost]
    public Task<IActionResult> Visibility(int id, bool isVisible, string? returnUrl) =>
        RunAsync(
            () => admin.SetReviewVisibilityAsync(id, isVisible),
            isVisible ? "Đã hiện lại đánh giá." : "Đã ẩn đánh giá. Điểm trung bình của sản phẩm đã được tính lại.",
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/admin/reviews");
}
