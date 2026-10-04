using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Inventory;

public sealed class InventoryService
{
    private readonly IApplicationDbContext _db;

    public InventoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// UC-51. Optimistic check: the update only runs while the stock still equals the value
    /// the staff member saw, so a sale made meanwhile is never overwritten (BR-17).
    /// </summary>
    public async Task UpdateStockAsync(int variantId, UpdateStockRequest request, CancellationToken ct)
    {
        if (request.StockQuantity < 0)
        {
            throw new ValidationException([new ValidationFailure("stockQuantity", "Stock cannot be negative.")]);
        }

        var exists = await _db.ProductVariants.AnyAsync(v => v.VariantId == variantId, ct);
        if (!exists)
        {
            throw new NotFoundException("Variant not found.");
        }

        var updated = await _db.ProductVariants
            .Where(v => v.VariantId == variantId && v.StockQuantity == request.ExpectedCurrentStock)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, request.StockQuantity), ct);

        if (updated == 0)
        {
            var current = await _db.ProductVariants
                .Where(v => v.VariantId == variantId)
                .Select(v => v.StockQuantity)
                .FirstAsync(ct);
            throw new ConflictException(
                ErrorCodes.StockChanged,
                $"The stock changed to {current} while you were editing. Review it and try again.",
                new { currentStock = current });
        }
    }

    /// <summary>UC-52. Variants with StockQuantity &lt;= LowStockThreshold, lowest stock first.</summary>
    public async Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(int? categoryId, bool includeHidden, CancellationToken ct)
    {
        var variants = _db.ProductVariants.AsNoTracking()
            .Where(v => v.StockQuantity <= v.LowStockThreshold);

        if (!includeHidden)
        {
            variants = variants.Where(v => v.IsActive && v.Product!.IsActive);
        }

        if (categoryId is int id)
        {
            variants = variants.Where(v => v.Product!.CategoryId == id);
        }

        return await variants
            .OrderBy(v => v.StockQuantity).ThenBy(v => v.Product!.ProductName)
            .Select(v => new LowStockItemDto(
                v.VariantId, v.ProductId, v.Product!.ProductName, v.Sku, v.Size, v.Color, v.StockQuantity, v.LowStockThreshold))
            .ToListAsync(ct);
    }
}
