namespace Daisy.Mvc.Models.Api;

public sealed record CategoryAdminNode(
    int Id,
    string Name,
    string Slug,
    int? ParentCategoryId,
    string? ImageUrl,
    int SortOrder,
    bool IsActive,
    int ProductCount,
    List<CategoryAdminNode> Children)
{
    public IEnumerable<(CategoryAdminNode Node, int Depth)> Flatten(int depth = 0) =>
        new[] { (this, depth) }.Concat(Children.SelectMany(c => c.Flatten(depth + 1)));
}

public sealed record CategoryInfo(int Id, string Name, string Slug, int? ParentCategoryId, string? ImageUrl, int SortOrder, bool IsActive);

public sealed record ProductAdminListItem(
    int Id,
    string Name,
    string Slug,
    string? Brand,
    int CategoryId,
    string CategoryName,
    decimal BasePrice,
    decimal? SalePrice,
    decimal EffectivePrice,
    bool IsOnSale,
    string? ThumbnailUrl,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    bool IsFeatured,
    bool IsActive,
    int VariantCount,
    int TotalStock,
    bool HasLowStock,
    DateTimeOffset CreatedAt);

public sealed record VariantAdmin(
    int Id,
    int ProductId,
    string Sku,
    string? Size,
    string? Color,
    string? ColorHex,
    decimal ExtraPrice,
    int StockQuantity,
    int LowStockThreshold,
    bool IsActive);

public sealed record ProductAdmin(
    int Id,
    int CategoryId,
    string Name,
    string Slug,
    string? Brand,
    string? Description,
    string? Material,
    string? GenderTarget,
    decimal BasePrice,
    decimal? SalePrice,
    DateTimeOffset? SaleStartAt,
    DateTimeOffset? SaleEndAt,
    bool IsOnSale,
    decimal AverageRating,
    int ReviewCount,
    int SoldCount,
    bool IsFeatured,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    List<VariantAdmin> Variants,
    List<ProductImage> Images);

public sealed record LowStockItem(int VariantId, int ProductId, string ProductName, string Sku, string? Size, string? Color, int StockQuantity, int LowStockThreshold);

public sealed record OrderAdminListItem(
    int Id,
    string OrderCode,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    string ReceiverPhone,
    OrderStatus OrderStatus,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed record OrderCustomer(int Id, string FullName, string Email, string? Phone);

public sealed record OrderAdminDetail : OrderDetail
{
    public OrderCustomer Customer { get; init; } = null!;

    public string? TransactionCode { get; init; }

    public List<OrderStatus> AllowedStatusTransitions { get; init; } = [];

    public List<string> AllowedPaymentActions { get; init; } = [];
}

public sealed record PaymentTransactionOrder(int Id, string OrderCode, OrderStatus OrderStatus, PaymentStatus PaymentStatus, decimal TotalAmount);

public sealed record ResolvedBy(int Id, string FullName);

public sealed record PaymentTransaction(
    int Id,
    string BankReference,
    string Provider,
    string AccountNumber,
    decimal Amount,
    string? TransferContent,
    DateTimeOffset TransferredAt,
    DateTimeOffset ReceivedAt,
    PaymentMatchResult MatchResult,
    PaymentTransactionOrder? Order,
    bool NeedsReview,
    PaymentResolution? Resolution,
    string? ResolutionNote,
    ResolvedBy? ResolvedBy,
    DateTimeOffset? ResolvedAt);

public sealed record Voucher(
    int Id,
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    int? UsageLimit,
    int UsedCount,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsActive,
    string State);

public sealed record VoucherOrder(int OrderId, string OrderCode, string CustomerName, decimal DiscountAmount, decimal TotalAmount, OrderStatus OrderStatus, DateTimeOffset CreatedAt);

public sealed record VoucherUsage(Voucher Voucher, decimal TotalDiscountAmount, PagedResult<VoucherOrder> Orders);

public sealed record ReviewAdminItem(
    int Id,
    int ProductId,
    string ProductName,
    int UserId,
    string UserFullName,
    int OrderItemId,
    int Rating,
    string? Comment,
    bool IsVisible,
    DateTimeOffset CreatedAt);

public sealed record UserListItem(int Id, string FullName, string Email, string? Phone, UserRole Role, UserStatus Status, DateTimeOffset CreatedAt);

public sealed record UserDetail(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    string? AvatarUrl,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    int OrderCount,
    decimal CompletedSpent,
    PagedResult<OrderSummary> Orders);

public sealed record RevenuePoint(string Period, decimal Revenue, int OrderCount);

public sealed record OrderStatusCount(OrderStatus Status, int Count);

public sealed record TopProduct(int ProductId, string ProductName, string? ThumbnailUrl, int QuantitySold, decimal Revenue);

public sealed record NewCustomerPoint(string Period, int Count);
