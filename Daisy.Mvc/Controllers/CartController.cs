using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-27 – UC-30. Works for guests (session cart) and signed-in users.</summary>
[Route("cart")]
public sealed class CartController(CartApiService cart) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Giỏ hàng";
        return View(await cart.GetAsync());
    }

    [HttpPost("items/{id:int}/quantity")]
    public Task<IActionResult> UpdateQuantity(int id, int quantity) =>
        RunAsync(() => cart.UpdateAsync(id, Math.Max(1, quantity)));

    [HttpPost("items/{id:int}/remove")]
    public Task<IActionResult> Remove(int id) =>
        RunAsync(() => cart.RemoveAsync(id), "Đã xóa sản phẩm khỏi giỏ.");

    [HttpPost("select")]
    public Task<IActionResult> Select(int[] itemIds, bool selected) =>
        RunAsync(() => cart.SelectAsync(itemIds, selected));

    private async Task<IActionResult> RunAsync(Func<Task> action, string? success = null)
    {
        try
        {
            await action();
            if (success is not null)
            {
                Flash(success);
            }
        }
        catch (ApiException ex) when (ex.Status is not System.Net.HttpStatusCode.Unauthorized)
        {
            Flash(ErrorMessages.For(ex), "bad");
        }

        return RedirectToAction(nameof(Index));
    }
}
