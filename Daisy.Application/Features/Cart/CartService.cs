using Daisy.Application.Common;
using Daisy.Application.Features.Products;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Cart;

public sealed class CartService
{
    private readonly IApplicationDbContext _db;
    private readonly CatalogVisibility _visibility;
    private readonly TimeProvider _time;

    public CartService(IApplicationDbContext db, CatalogVisibility visibility, TimeProvider time)
    {
        _db = db;
        _visibility = visibility;
        _time = time;
    }

    /// <summary>UC-27. Prices are read live (BR-21).</summary>
    public async Task<CartDto> GetCartAsync(CartOwner owner, CancellationToken ct)
    {
        var lines = await LoadLinesAsync(owner, ct);
        return await BuildCartAsync(lines, ct);
    }

    /// <summary>UC-26. Adds a line or increases the existing line of the same variant (BR-19, BR-20).</summary>
    public async Task<CartSummaryDto> AddItemAsync(CartOwner owner, AddCartItemRequest request, CancellationToken ct)
    {
        ValidateQuantity(request.Quantity);
        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == request.VariantId, ct);

        if (variant?.Product is null || !await _visibility.IsVisibleAsync(variant.Product, variant, ct))
        {
            throw new NotFoundException("This product is not available.");
        }

        var line = await OwnedLines(owner).FirstOrDefaultAsync(c => c.VariantId == variant.VariantId, ct);
        var existing = line?.Quantity ?? 0;
        var newQuantity = existing + request.Quantity;
        if (newQuantity > variant.StockQuantity)
        {
            throw InsufficientStock(variant, existing);
        }

        if (line is null)
        {
            _db.CartItems.Add(new CartItem
            {
                UserId = owner.UserId,
                SessionId = owner.UserId is null ? owner.SessionId : null,
                VariantId = variant.VariantId,
                Quantity = newQuantity,
                IsSelected = true,
                CreatedAt = _time.GetVietnamNow(),
            });
        }
        else
        {
            line.Quantity = newQuantity;
        }

        await _db.SaveChangesAsync(ct);
        return await GetSummaryAsync(owner, ct);
    }

    /// <summary>UC-28.</summary>
    public async Task<CartDto> UpdateQuantityAsync(CartOwner owner, int cartItemId, UpdateCartItemRequest request, CancellationToken ct)
    {
        ValidateQuantity(request.Quantity);
        var line = await OwnedLines(owner)
            .Include(c => c.Variant!).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.CartItemId == cartItemId, ct)
            ?? throw new NotFoundException("Cart item not found.");

        var variant = line.Variant!;
        if (!await _visibility.IsVisibleAsync(variant.Product!, variant, ct))
        {
            throw new ConflictException(ErrorCodes.ItemUnavailable, "This item is no longer available.");
        }

        if (request.Quantity > variant.StockQuantity)
        {
            throw InsufficientStock(variant, 0);
        }

        line.Quantity = request.Quantity;
        await _db.SaveChangesAsync(ct);
        return await GetCartAsync(owner, ct);
    }

    /// <summary>UC-29.</summary>
    public async Task<CartDto> RemoveItemAsync(CartOwner owner, int cartItemId, CancellationToken ct)
    {
        var line = await OwnedLines(owner).FirstOrDefaultAsync(c => c.CartItemId == cartItemId, ct)
            ?? throw new NotFoundException("Cart item not found.");

        _db.CartItems.Remove(line);
        await _db.SaveChangesAsync(ct);
        return await GetCartAsync(owner, ct);
    }

    /// <summary>UC-30. Ids that the caller does not own are ignored.</summary>
    public async Task<CartDto> SetSelectionAsync(CartOwner owner, CartSelectionRequest request, CancellationToken ct)
    {
        var ids = (request.ItemIds ?? []).Distinct().ToList();
        var lines = await OwnedLines(owner)
            .Include(c => c.Variant!).ThenInclude(v => v.Product)
            .Where(c => ids.Contains(c.CartItemId))
            .ToListAsync(ct);

        if (request.Selected)
        {
            foreach (var line in lines)
            {
                if (!await _visibility.IsVisibleAsync(line.Variant!.Product!, line.Variant, ct) || line.Variant.StockQuantity < line.Quantity)
                {
                    throw new ConflictException(ErrorCodes.ItemUnavailable, "This item is unavailable.", new { cartItemId = line.CartItemId });
                }
            }
        }

        foreach (var line in lines)
        {
            line.IsSelected = request.Selected;
        }

        await _db.SaveChangesAsync(ct);
        return await GetCartAsync(owner, ct);
    }

    /// <summary>UC-31. Moves the guest lines into the user cart (BR-22), capped at stock (BR-20).</summary>
    public async Task MergeGuestCartAsync(string sessionId, int userId, CancellationToken ct)
    {
        var guestLines = await _db.CartItems
            .Include(c => c.Variant)
            .Where(c => c.SessionId == sessionId && c.UserId == null)
            .ToListAsync(ct);

        if (guestLines.Count == 0)
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var userLines = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync(ct);

        foreach (var guest in guestLines)
        {
            var stock = guest.Variant?.StockQuantity ?? 0;
            var userLine = userLines.FirstOrDefault(u => u.VariantId == guest.VariantId);
            if (userLine is not null)
            {
                // Without stock the user's own line is kept as it is and shown as unavailable.
                var merged = Math.Min(userLine.Quantity + guest.Quantity, stock);
                if (merged >= 1)
                {
                    userLine.Quantity = merged;
                }

                _db.CartItems.Remove(guest);
            }
            else
            {
                var quantity = Math.Min(guest.Quantity, stock);
                if (quantity <= 0)
                {
                    _db.CartItems.Remove(guest);
                    continue;
                }

                guest.UserId = userId;
                guest.SessionId = null;
                guest.Quantity = quantity;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal IQueryable<CartItem> OwnedLines(CartOwner owner) =>
        owner.UserId is int userId
            ? _db.CartItems.Where(c => c.UserId == userId)
            : _db.CartItems.Where(c => c.SessionId == owner.SessionId && c.UserId == null);

    internal Task<List<CartItem>> LoadLinesAsync(CartOwner owner, CancellationToken ct) =>
        OwnedLines(owner)
            .AsNoTracking()
            .Include(c => c.Variant!).ThenInclude(v => v.Product!).ThenInclude(p => p.Images)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.CartItemId)
            .ToListAsync(ct);

    internal async Task<CartDto> BuildCartAsync(IReadOnlyList<CartItem> lines, CancellationToken ct)
    {
        var now = _time.GetVietnamNow();
        var visibleCategories = await _visibility.GetVisibleCategoryIdsAsync(ct);
        var items = lines.Select(line => ToDto(line, now, visibleCategories)).ToList();
        var selected = items.Where(i => i.IsSelected && i.IsAvailable).ToList();
        return new CartDto(
            items,
            items.Count,
            items.Sum(i => i.Quantity),
            selected.Count,
            selected.Sum(i => i.LineTotal));
    }

    internal static CartItemDto ToDto(CartItem line, DateTime now, HashSet<int> visibleCategories)
    {
        var variant = line.Variant!;
        var product = variant.Product!;
        var hidden = !variant.IsActive || !product.IsActive || !visibleCategories.Contains(product.CategoryId);
        var reason = hidden ? CartUnavailableReason.Hidden
            : variant.StockQuantity <= 0 ? CartUnavailableReason.OutOfStock
            : line.Quantity > variant.StockQuantity ? CartUnavailableReason.NotEnoughStock
            : null;
        var unitPrice = variant.GetPrice(now);

        return new CartItemDto(
            line.CartItemId,
            variant.VariantId,
            product.ProductId,
            product.Slug,
            product.ProductName,
            ProductImageSelector.Pick(product.Images, variant.VariantId),
            variant.Size,
            variant.Color,
            unitPrice,
            product.BasePrice + variant.ExtraPrice,
            line.Quantity,
            unitPrice * line.Quantity,
            variant.StockQuantity,
            line.IsSelected,
            reason is null,
            reason);
    }

    private async Task<CartSummaryDto> GetSummaryAsync(CartOwner owner, CancellationToken ct)
    {
        var quantities = await OwnedLines(owner).Select(c => c.Quantity).ToListAsync(ct);
        return new CartSummaryDto(quantities.Count, quantities.Sum());
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 1)
        {
            throw new ValidationException([new ValidationFailure("quantity", "Quantity must be at least 1.")]);
        }
    }

    private static ConflictException InsufficientStock(ProductVariant variant, int alreadyInCart) =>
        new(
            ErrorCodes.InsufficientStock,
            alreadyInCart > 0
                ? $"Only {variant.StockQuantity} items left in stock and {alreadyInCart} are already in your cart."
                : $"Only {variant.StockQuantity} items left in stock.",
            new
            {
                variantId = variant.VariantId,
                availableStock = Math.Max(0, variant.StockQuantity - alreadyInCart),
            });
}
