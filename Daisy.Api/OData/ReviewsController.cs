using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Daisy.Api.OData;

/// <summary>GET /odata/reviews — UC-61 (includes hidden reviews).</summary>
[Authorize(Policy = Policies.Staff)]
public sealed class ReviewsController : ODataController
{
    private readonly ReviewService _reviews;

    public ReviewsController(ReviewService reviews)
    {
        _reviews = reviews;
    }

    [DaisyEnableQuery]
    public IQueryable<ReviewAdminDto> Get() => _reviews.QueryAdminList();
}
