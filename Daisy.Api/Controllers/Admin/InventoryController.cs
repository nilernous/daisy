using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-51, UC-52.</summary>
[Route("api/admin")]
[Authorize(Policy = Policies.Staff)]
public sealed class InventoryController : ApiControllerBase
{
    private readonly InventoryService _inventory;

    public InventoryController(InventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpPut("variants/{id:int}/stock")]
    public async Task<IActionResult> UpdateStock(int id, UpdateStockRequest request, CancellationToken ct)
    {
        await _inventory.UpdateStockAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("inventory/low-stock")]
    public async Task<ActionResult<IReadOnlyList<LowStockItemDto>>> GetLowStock(
        [FromQuery] int? categoryId,
        [FromQuery] bool includeHidden,
        CancellationToken ct) =>
        Ok(await _inventory.GetLowStockAsync(categoryId, includeHidden, ct));
}
