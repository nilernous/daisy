namespace Daisy.Application.Features.Inventory;

public sealed record UpdateStockRequest(int StockQuantity, int ExpectedCurrentStock);

public sealed record LowStockItemDto(
    int VariantId,
    int ProductId,
    string ProductName,
    string Sku,
    string? Size,
    string? Color,
    int StockQuantity,
    int LowStockThreshold);
