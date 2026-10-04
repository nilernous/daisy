using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-62. The review list is GET /odata/reviews (UC-61).</summary>
[Route("api/admin/reviews")]
[Authorize(Policy = Policies.Staff)]
public sealed class ReviewsController : ApiControllerBase
{
    private readonly ReviewService _reviews;

    public ReviewsController(ReviewService reviews)
    {
        _reviews = reviews;
    }

    [HttpPatch("{id:int}/visibility")]
    public async Task<IActionResult> SetReviewVisibility(int id, SetVisibleRequest request, CancellationToken ct)
    {
        await _reviews.SetVisibilityAsync(id, request.IsVisible, ct);
        return NoContent();
    }
}
