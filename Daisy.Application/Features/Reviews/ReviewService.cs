using Daisy.Application.Common;
using Daisy.Application.Features.Products;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Reviews;

public sealed class ReviewService
{
    private readonly IApplicationDbContext _db;
    private readonly CatalogVisibility _visibility;
    private readonly IDatabaseErrorClassifier _dbErrors;
    private readonly TimeProvider _time;
    private readonly IValidator<CreateReviewRequest> _validator;

    public ReviewService(
        IApplicationDbContext db,
        CatalogVisibility visibility,
        IDatabaseErrorClassifier dbErrors,
        TimeProvider time,
        IValidator<CreateReviewRequest> validator)
    {
        _db = db;
        _visibility = visibility;
        _dbErrors = dbErrors;
        _time = time;
        _validator = validator;
    }

    /// <summary>
    /// UC-41. Only for an item of the customer's own Completed order, once per item (BR-37).
    /// User and product come from the order item, not from the request.
    /// </summary>
    public async Task<ReviewDto> CreateAsync(int userId, CreateReviewRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var item = await _db.OrderItems.AsNoTracking()
            .Include(i => i.Order)
            .Include(i => i.Variant)
            .FirstOrDefaultAsync(i => i.OrderItemId == request.OrderItemId && i.Order!.UserId == userId, ct)
            ?? throw new NotFoundException("Order item not found.");

        if (item.Order!.OrderStatus != OrderStatus.Completed)
        {
            throw new ConflictException(ErrorCodes.ReviewNotAllowed, "You can review this item after the order is completed.");
        }

        if (await _db.Reviews.AnyAsync(r => r.OrderItemId == item.OrderItemId, ct))
        {
            throw new ConflictException(ErrorCodes.AlreadyReviewed, "You have already reviewed this item.");
        }

        var review = new Review
        {
            UserId = userId,
            ProductId = item.Variant!.ProductId,
            OrderItemId = item.OrderItemId,
            Rating = (byte)request.Rating,
            Comment = ValidationRules.TrimToNull(request.Comment),
            IsVisible = true,
            CreatedAt = _time.GetVietnamNow(),
        };
        _db.Reviews.Add(review);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (_dbErrors.IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException(ErrorCodes.AlreadyReviewed, "You have already reviewed this item.");
        }

        await RecalculateRatingAsync(review.ProductId, ct);
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId, ct);
        return new ReviewDto(review.ReviewId, review.Rating, review.Comment, DisplayName(user.FullName), user.AvatarUrl,
            item.SizeSnapshot, item.ColorSnapshot, review.CreatedAt);
    }

    /// <summary>UC-42. Visible reviews only (BR-39).</summary>
    public async Task<ProductReviewsDto> GetProductReviewsAsync(int productId, ProductReviewsQuery query, CancellationToken ct)
    {
        query.ValidatePage();
        if (query.Rating is < 1 or > 5)
        {
            throw new ValidationException([new ValidationFailure("rating", "Rating must be between 1 and 5.")]);
        }

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null || !await _visibility.IsVisibleAsync(product, null, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var visible = _db.Reviews.AsNoTracking().Where(r => r.ProductId == productId && r.IsVisible);
        var counts = await visible.GroupBy(r => r.Rating).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var breakdown = Enumerable.Range(1, 5).Reverse()
            .ToDictionary(r => r.ToString(), r => counts.FirstOrDefault(c => c.Key == r)?.Count ?? 0);

        var filtered = query.Rating is int rating ? visible.Where(r => r.Rating == rating) : visible;
        var page = await filtered
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewId)
            .Select(r => new
            {
                r.ReviewId,
                r.Rating,
                r.Comment,
                r.User!.FullName,
                r.User.AvatarUrl,
                r.OrderItem!.SizeSnapshot,
                r.OrderItem.ColorSnapshot,
                r.CreatedAt,
            })
            .ToPagedResultAsync(query, ct);

        return new ProductReviewsDto(
            product.AverageRating,
            product.ReviewCount,
            breakdown,
            page.Map(r => new ReviewDto(r.ReviewId, r.Rating, r.Comment, DisplayName(r.FullName), r.AvatarUrl, r.SizeSnapshot, r.ColorSnapshot, r.CreatedAt)));
    }

    /// <summary>Query behind GET /odata/reviews (UC-61).</summary>
    public IQueryable<ReviewAdminDto> QueryAdminList() =>
        _db.Reviews.AsNoTracking().Select(r => new ReviewAdminDto
        {
            Id = r.ReviewId,
            ProductId = r.ProductId,
            ProductName = r.Product!.ProductName,
            UserId = r.UserId,
            UserFullName = r.User!.FullName,
            OrderItemId = r.OrderItemId,
            Rating = r.Rating,
            Comment = r.Comment,
            IsVisible = r.IsVisible,
            CreatedAt = r.CreatedAt,
        });

    /// <summary>UC-62. Hidden reviews stay in the database (BR-39).</summary>
    public async Task SetVisibilityAsync(int reviewId, bool isVisible, CancellationToken ct)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct)
            ?? throw new NotFoundException("Review not found.");
        review.IsVisible = isVisible;
        await _db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(review.ProductId, ct);
    }

    /// <summary>AverageRating and ReviewCount from visible reviews, rounded to 2 decimals (BR-38).</summary>
    private async Task RecalculateRatingAsync(int productId, CancellationToken ct)
    {
        var stats = await _db.Reviews
            .Where(r => r.ProductId == productId && r.IsVisible)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Rating) })
            .FirstOrDefaultAsync(ct);

        var count = stats?.Count ?? 0;
        var average = stats is null ? 0m : Math.Round(stats.Average, 2, MidpointRounding.AwayFromZero);
        await _db.Products
            .Where(p => p.ProductId == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.AverageRating, average)
                .SetProperty(p => p.ReviewCount, count), ct);
    }

    /// <summary>"Nguyen Van A" is shown as "Nguyen V. A" to keep reviewers' full names private.</summary>
    internal static string DisplayName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
        {
            return fullName.Trim();
        }

        var middle = parts[1..^1].Select(p => char.ToUpperInvariant(p[0]) + ".");
        return string.Join(' ', new[] { parts[0] }.Concat(middle).Append(parts[^1]));
    }
}
