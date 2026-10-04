using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daisy.Mvc.Models.Api;

public enum UserRole { Customer, Staff, Admin }

public enum UserStatus { Active, Locked }

public enum OrderStatus { Pending, Confirmed, Shipping, Completed, Canceled }

public enum PaymentMethod { COD, Online }

public enum PaymentStatus { Unpaid, Paid, Failed, Refunded }

public enum DiscountType { Percent, Fixed }

public enum PaymentMatchResult { Matched, NoOrder, Underpaid, OrderNotPayable }

public enum PaymentResolution { Applied, Refunded, Ignored }

/// <summary>REST list envelope (API_DESIGN §2.4).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResult<T> Empty { get; } = new([], 1, 20, 0, 0);
}

/// <summary>OData list envelope (API_DESIGN §7).</summary>
public sealed class ODataPage<T>
{
    [JsonPropertyName("@odata.count")]
    public int? Count { get; init; }

    [JsonPropertyName("value")]
    public List<T> Value { get; init; } = [];
}

/// <summary>The API error body (API_DESIGN §4.1).</summary>
public sealed record ApiError(
    int Status,
    string Code,
    string Message,
    Dictionary<string, string[]>? Errors,
    JsonElement? Details,
    string? TraceId);

public sealed record MessageResult(string Message);
