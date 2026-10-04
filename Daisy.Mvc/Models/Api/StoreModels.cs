namespace Daisy.Mvc.Models.Api;

// ---- Auth & profile ---------------------------------------------------------------------

public sealed record UserSummary(int Id, string FullName, string Email, UserRole Role, string? AvatarUrl);

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummary User);

public sealed record Profile(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    string? AvatarUrl,
    UserRole Role,
    DateTimeOffset CreatedAt);

public sealed record Address(
    int Id,
    string ReceiverName,
    string ReceiverPhone,
    string Province,
    string District,
    string Ward,
    string StreetAddress,
    string FullAddress,
    bool IsDefault);

// ---- Catalog -------------------------------------------------------------------------------

public sealed record CategoryNode(int Id, string Name, string Slug, string? ImageUrl, int SortOrder, List<CategoryNode> Children)
{
    public IEnumerable<int> SelfAndDescendantIds() => new[] { Id }.Concat(Children.SelectMany(c => c.SelfAndDescendantIds()));
}

public sealed record VariantOption(string? Size, string? Color);

public sealed record ProductListItem(
    int Id,
    string Name,
    string Slug,
    string? Brand,
    int CategoryId,
    string? GenderTarget,
    decimal BasePrice,
    decimal? SalePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    DateTimeOffset? SaleEndAt,
    string? ThumbnailUrl,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    bool IsFeatured,
    DateTimeOffset CreatedAt,
    List<VariantOption>? Variants);

public sealed record CategoryRef(int Id, string Name, string Slug);

public sealed record ProductVariant(
    int Id,
    string Sku,
    string? Size,
    string? Color,
    string? ColorHex,
    decimal ExtraPrice,
    decimal Price,
    int StockQuantity,
    bool InStock,
    bool IsLowStock);

public sealed record ProductImage(int Id, string Url, bool IsThumbnail, int SortOrder, int? VariantId, string? Color);

public sealed record ProductDetail(
    int Id,
    string Slug,
    string Name,
    string? Brand,
    string? Description,
    string? Material,
    string? GenderTarget,
    CategoryRef Category,
    decimal BasePrice,
    decimal? SalePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    DateTimeOffset? SaleEndAt,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    List<ProductVariant> Variants,
    List<ProductImage> Images);

public sealed record Review(
    int Id,
    int Rating,
    string? Comment,
    string UserFullName,
    string? UserAvatarUrl,
    string? Size,
    string? Color,
    DateTimeOffset CreatedAt);

public sealed record ProductReviews(
    decimal AverageRating,
    int ReviewCount,
    Dictionary<string, int> RatingBreakdown,
    PagedResult<Review> Reviews);

public sealed record WishlistItem(
    int ProductId,
    string Slug,
    string Name,
    string? ThumbnailUrl,
    decimal BasePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    bool IsAvailable,
    DateTimeOffset AddedAt);

// ---- Cart & checkout ----------------------------------------------------------------------------

public sealed record CartItem(
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

public sealed record Cart(List<CartItem> Items, int ItemCount, int TotalQuantity, int SelectedCount, decimal SelectedSubtotal)
{
    public static Cart Empty { get; } = new([], 0, 0, 0, 0);
}

public sealed record CartSummary(int ItemCount, int TotalQuantity);

public sealed record CheckoutItem(
    int CartItemId,
    int VariantId,
    string ProductName,
    string? Size,
    string? Color,
    string? ThumbnailUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record VoucherInfo(string Code, string Name, DiscountType DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount);

public sealed record CheckoutTotals(decimal SubtotalAmount, decimal DiscountAmount, decimal ShippingFee, decimal TotalAmount, VoucherInfo? Voucher);

public sealed record Checkout(
    List<CheckoutItem> Items,
    List<Address> Addresses,
    int? DefaultAddressId,
    List<PaymentMethod> PaymentMethods,
    CheckoutTotals Totals,
    bool CanPlaceOrder);

public sealed record PlacedOrder(
    int OrderId,
    string OrderCode,
    OrderStatus OrderStatus,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    DateTimeOffset? PaymentExpiresAt,
    string? PaymentQrUrl);

// ---- Orders ---------------------------------------------------------------------------------------

public sealed record OrderSummary(
    int Id,
    string OrderCode,
    DateTimeOffset CreatedAt,
    OrderStatus OrderStatus,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    int ItemCount,
    string? ThumbnailUrl);

public sealed record OrderItem(
    int OrderItemId,
    int ProductId,
    string ProductSlug,
    string ProductName,
    string? Size,
    string? Color,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool CanReview,
    int? ReviewId);

public record OrderDetail
{
    public int Id { get; init; }

    public string OrderCode { get; init; } = string.Empty;

    public OrderStatus OrderStatus { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public string ReceiverName { get; init; } = string.Empty;

    public string ReceiverPhone { get; init; } = string.Empty;

    public string ShippingAddress { get; init; } = string.Empty;

    public string? CustomerNote { get; init; }

    public List<OrderItem> Items { get; init; } = [];

    public string? VoucherCode { get; init; }

    public decimal SubtotalAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal TotalAmount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PaymentExpiresAt { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? CanceledAt { get; init; }

    public string? CancelReason { get; init; }

    public bool CanCancel { get; init; }

    public bool CanPayWithQr { get; init; }
}

public sealed record CanceledOrder(int Id, string OrderCode, OrderStatus OrderStatus, PaymentStatus PaymentStatus, DateTimeOffset? CanceledAt, bool RefundRequired);

public sealed record PaymentQr(
    int OrderId,
    string OrderCode,
    decimal Amount,
    string BankBin,
    string BankName,
    string AccountNumber,
    string AccountName,
    string TransferContent,
    string QrImageUrl,
    DateTimeOffset? ExpiresAt);
