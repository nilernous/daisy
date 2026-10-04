using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Wishlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-23 – UC-25.</summary>
[Route("api/wishlist")]
[Authorize(Policy = Policies.Customer)]
public sealed class WishlistController : ApiControllerBase
{
    private readonly WishlistService _wishlist;

    public WishlistController(WishlistService wishlist)
    {
        _wishlist = wishlist;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WishlistItemDto>>> GetWishlist(CancellationToken ct) =>
        Ok(await _wishlist.GetAsync(CurrentUserId, ct));

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> AddToWishlist(int productId, CancellationToken ct)
    {
        await _wishlist.AddAsync(CurrentUserId, productId, ct);
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> RemoveFromWishlist(int productId, CancellationToken ct)
    {
        await _wishlist.RemoveAsync(CurrentUserId, productId, ct);
        return NoContent();
    }
}
