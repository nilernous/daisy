using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Products;

/// <summary>UC-47 Manage Variants. Variants are hidden, never deleted (BR-11).</summary>
public sealed class ProductVariantService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<VariantRequest> _validator;

    public ProductVariantService(IApplicationDbContext db, IValidator<VariantRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<VariantAdminDto> CreateAsync(int productId, VariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        if (!await _db.Products.AnyAsync(p => p.ProductId == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        var size = ValidationRules.TrimToNull(request.Size);
        var color = ValidationRules.TrimToNull(request.Color);
        await EnsureUniqueAsync(productId, null, sku, size, color, ct);

        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = sku,
            Size = size,
            Color = color,
            ColorHex = ValidationRules.TrimToNull(request.ColorHex)?.ToUpperInvariant(),
            ExtraPrice = request.ExtraPrice,
            StockQuantity = request.StockQuantity ?? 0,
            LowStockThreshold = request.LowStockThreshold,
            IsActive = true,
        };
        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync(ct);
        return ProductService.ToVariantDto(variant);
    }

    /// <summary>Stock changes go through the inventory endpoint (UC-51), not here.</summary>
    public async Task<VariantAdminDto> UpdateAsync(int variantId, VariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var variant = await FindAsync(variantId, ct);

        var sku = request.Sku.Trim().ToUpperInvariant();
        var size = ValidationRules.TrimToNull(request.Size);
        var color = ValidationRules.TrimToNull(request.Color);
        await EnsureUniqueAsync(variant.ProductId, variantId, sku, size, color, ct);

        variant.Sku = sku;
        variant.Size = size;
        variant.Color = color;
        variant.ColorHex = ValidationRules.TrimToNull(request.ColorHex)?.ToUpperInvariant();
        variant.ExtraPrice = request.ExtraPrice;
        variant.LowStockThreshold = request.LowStockThreshold;
        await _db.SaveChangesAsync(ct);
        return ProductService.ToVariantDto(variant);
    }

    public async Task SetVisibilityAsync(int variantId, bool isActive, CancellationToken ct)
    {
        var variant = await FindAsync(variantId, ct);
        variant.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ProductVariant> FindAsync(int variantId, CancellationToken ct) =>
        await _db.ProductVariants.FirstOrDefaultAsync(v => v.VariantId == variantId, ct)
        ?? throw new NotFoundException("Variant not found.");

    /// <summary>SKU is unique; size + color is unique per product (BR-12).</summary>
    private async Task EnsureUniqueAsync(int productId, int? exceptId, string sku, string? size, string? color, CancellationToken ct)
    {
        if (await _db.ProductVariants.AnyAsync(v => v.Sku == sku && v.VariantId != exceptId, ct))
        {
            throw new ConflictException(ErrorCodes.SkuAlreadyExists, "Another variant already uses this SKU.");
        }

        if (await _db.ProductVariants.AnyAsync(
                v => v.ProductId == productId && v.Size == size && v.Color == color && v.VariantId != exceptId, ct))
        {
            throw new ConflictException(ErrorCodes.VariantOptionExists, "This product already has a variant with the same size and color.");
        }
    }
}
