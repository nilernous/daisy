using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Daisy.Api.OData;

// OData is read-only: list queries only (ARCHITECTURE §23). Business processes stay REST.

/// <summary>GET /odata/products — UC-14, UC-16 – UC-22.</summary>
[AllowAnonymous]
public sealed class ProductsController : ODataController
{
    private readonly ProductService _products;

    public ProductsController(ProductService products)
    {
        _products = products;
    }

    [DaisyEnableQuery]
    public Task<IQueryable<ProductListDto>> Get(CancellationToken ct) => _products.QueryStorefrontAsync(ct);
}
