namespace Daisy.Application.Features.Cart;

/// <summary>Who owns a cart: a signed-in user or a guest session (BR-19).</summary>
public sealed record CartOwner(int? UserId, string? SessionId)
{
    public static CartOwner ForUser(int userId) => new(userId, null);

    public static CartOwner ForSession(string sessionId) => new(null, sessionId);
}

public sealed record AddCartItemRequest(int VariantId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartSelectionRequest(IReadOnlyList<int> ItemIds, bool Selected);

public static class CartUnavailableReason
{
    public const string Hidden = "Hidden";
    public const string OutOfStock = "OutOfStock";
    public const string NotEnoughStock = "NotEnoughStock";
}

public sealed record CartItemDto(
    int Id,
    int VariantId,
    int ProductId,
    string ProductSlug,
    string ProductName,
    string? ThumbnailUrl,
    string? Size,
    string? Color,
    decimal UnitPrice,
    decimal BasePrice,
    int Quantity,
    decimal LineTotal,
    int StockQuantity,
    bool IsSelected,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record CartDto(
    IReadOnlyList<CartItemDto> Items,
    int ItemCount,
    int TotalQuantity,
    int SelectedCount,
    decimal SelectedSubtotal);

public sealed record CartSummaryDto(int ItemCount, int TotalQuantity);
