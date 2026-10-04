using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-51, UC-52.</summary>
public sealed class InventoryController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index(bool includeHidden = false)
    {
        ViewData["Title"] = "Sắp hết hàng";
        ViewData["IncludeHidden"] = includeHidden;
        return View(await admin.GetLowStockAsync(includeHidden));
    }

    /// <summary>The expected current stock protects against overwriting a sale made meanwhile.</summary>
    [HttpPost]
    public Task<IActionResult> UpdateStock(int variantId, int stockQuantity, int expectedCurrentStock, string? returnUrl) =>
        RunAsync(
            () => admin.UpdateStockAsync(variantId, stockQuantity, expectedCurrentStock),
            "Đã cập nhật tồn kho.",
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/admin/inventory");
}
