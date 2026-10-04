using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-41.</summary>
[Route("api/reviews")]
[Authorize(Policy = Policies.Customer)]
public sealed class ReviewsController : ApiControllerBase
{
    private readonly ReviewService _reviews;

    public ReviewsController(ReviewService reviews)
    {
        _reviews = reviews;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview(CreateReviewRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _reviews.CreateAsync(CurrentUserId, request, ct));
}
