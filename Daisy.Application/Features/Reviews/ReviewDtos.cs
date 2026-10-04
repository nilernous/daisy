using Daisy.Application.Common;

namespace Daisy.Application.Features.Reviews;

public sealed record CreateReviewRequest(int OrderItemId, int Rating, string? Comment);

public sealed record ReviewDto(
    int Id,
    int Rating,
    string? Comment,
    string UserFullName,
    string? UserAvatarUrl,
    string? Size,
    string? Color,
    DateTime CreatedAt);

public sealed record ProductReviewsQuery : PageRequest
{
    public int? Rating { get; init; }
}

public sealed record ProductReviewsDto(
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyDictionary<string, int> RatingBreakdown,
    PagedResult<ReviewDto> Reviews);

/// <summary>OData entity of /odata/reviews (API_DESIGN §7.3); includes hidden reviews.</summary>
public sealed class ReviewAdminDto
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public int UserId { get; init; }

    public string UserFullName { get; init; } = string.Empty;

    public int OrderItemId { get; init; }

    public int Rating { get; init; }

    public string? Comment { get; init; }

    public bool IsVisible { get; init; }

    public DateTime CreatedAt { get; init; }
}
