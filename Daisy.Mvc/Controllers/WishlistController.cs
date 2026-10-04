using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-23 – UC-25.</summary>
[Route("wishlist")]
[Authorize(Roles = "Customer")]
public sealed class WishlistController(ProfileApiService profile) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Sản phẩm yêu thích";
        return View(await profile.GetWishlistAsync());
    }

    [HttpPost("{productId:int}/remove")]
    public async Task<IActionResult> Remove(int productId)
    {
        await profile.RemoveFromWishlistAsync(productId);
        Flash("Đã bỏ khỏi danh sách yêu thích.");
        return RedirectToAction(nameof(Index));
    }
}
