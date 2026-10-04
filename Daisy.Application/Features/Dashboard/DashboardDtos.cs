using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Dashboard;

public sealed record DashboardQuery(DateOnly? From, DateOnly? To, string? GroupBy, int? Top);

public sealed record RevenuePointDto(string Period, decimal Revenue, int OrderCount);

public sealed record OrderStatusCountDto(OrderStatus Status, int Count);

public sealed record TopProductDto(int ProductId, string ProductName, string? ThumbnailUrl, int QuantitySold, decimal Revenue);

public sealed record NewCustomerPointDto(string Period, int Count);
