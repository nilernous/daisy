using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-26 – UC-30. Bearer token or X-Cart-Session (API_DESIGN §3.4).</summary>
[Route("api/cart")]
[AllowAnonymous]
public sealed class CartController : ApiControllerBase
{
    private readonly CartService _cart;

    public CartController(CartService cart)
    {
        _cart = cart;
    }

    /// <summary>Without a token or session, an empty cart is returned.</summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart(CancellationToken ct)
    {
        var owner = ResolveCartOwner(required: false);
        return owner is null ? new CartDto([], 0, 0, 0, 0) : await _cart.GetCartAsync(owner, ct);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartSummaryDto>> AddCartItem(AddCartItemRequest request, CancellationToken ct) =>
        await _cart.AddItemAsync(ResolveCartOwner()!, request, ct);

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<CartDto>> UpdateCartItem(int id, UpdateCartItemRequest request, CancellationToken ct) =>
        await _cart.UpdateQuantityAsync(ResolveCartOwner()!, id, request, ct);

    [HttpDelete("items/{id:int}")]
    public async Task<ActionResult<CartDto>> RemoveCartItem(int id, CancellationToken ct) =>
        await _cart.RemoveItemAsync(ResolveCartOwner()!, id, ct);

    [HttpPut("selection")]
    public async Task<ActionResult<CartDto>> SetCartSelection(CartSelectionRequest request, CancellationToken ct) =>
        await _cart.SetSelectionAsync(ResolveCartOwner()!, request, ct);
}
