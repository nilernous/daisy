using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Products;
using Daisy.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-15 and UC-42. Product lists are served by OData (GET /odata/products).</summary>
[Route("api/products")]
[AllowAnonymous]
public sealed class ProductsController : ApiControllerBase
{
    private readonly ProductService _products;
    private readonly ReviewService _reviews;

    public ProductsController(ProductService products, ReviewService reviews)
    {
        _products = products;
        _reviews = reviews;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetProductDetail(string slug, CancellationToken ct) =>
        await _products.GetDetailAsync(slug, ct);

    [HttpGet("{productId:int}/reviews")]
    public async Task<ActionResult<ProductReviewsDto>> GetProductReviews(int productId, [FromQuery] ProductReviewsQuery query, CancellationToken ct) =>
        await _reviews.GetProductReviewsAsync(productId, query, ct);
}
