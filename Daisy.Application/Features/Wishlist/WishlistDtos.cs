namespace Daisy.Application.Features.Wishlist;

public sealed record WishlistItemDto(
    int ProductId,
    string Slug,
    string Name,
    string? ThumbnailUrl,
    decimal BasePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    bool IsAvailable,
    DateTime AddedAt);
