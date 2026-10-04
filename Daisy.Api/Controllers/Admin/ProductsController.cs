using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-45 – UC-50: products, variants and images.</summary>
[Route("api/admin")]
[Authorize(Policy = Policies.Staff)]
public sealed class ProductsController : ApiControllerBase
{
    private readonly ProductService _products;
    private readonly ProductVariantService _variants;
    private readonly ProductImageService _images;

    public ProductsController(ProductService products, ProductVariantService variants, ProductImageService images)
    {
        _products = products;
        _variants = variants;
        _images = images;
    }

    // ---- Products ------------------------------------------------------------------

    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<ProductAdminListDto>>> GetAdminProducts([FromQuery] AdminProductQuery query, CancellationToken ct) =>
        await _products.GetAdminListAsync(query, ct);

    [HttpGet("products/{id:int}")]
    public async Task<ActionResult<ProductAdminDto>> GetAdminProduct(int id, CancellationToken ct) =>
        await _products.GetAdminAsync(id, ct);

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(ProductRequest request, CancellationToken ct)
    {
        var product = await _products.CreateAsync(request, ct);
        return Created($"/api/admin/products/{product.Id}", product);
    }

    [HttpPut("products/{id:int}")]
    public async Task<ActionResult<ProductAdminDto>> UpdateProduct(int id, ProductRequest request, CancellationToken ct) =>
        await _products.UpdateAsync(id, request, ct);

    [HttpPatch("products/{id:int}/visibility")]
    public async Task<IActionResult> SetProductVisibility(int id, SetActiveRequest request, CancellationToken ct)
    {
        await _products.SetVisibilityAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpPatch("products/{id:int}/featured")]
    public async Task<IActionResult> SetProductFeatured(int id, SetFeaturedRequest request, CancellationToken ct)
    {
        await _products.SetFeaturedAsync(id, request.IsFeatured, ct);
        return NoContent();
    }

    [HttpPut("products/{id:int}/sale")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetProductSale(int id, SaleRequest request, CancellationToken ct)
    {
        await _products.SetSaleAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("products/{id:int}/sale")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> ClearProductSale(int id, CancellationToken ct)
    {
        await _products.ClearSaleAsync(id, ct);
        return NoContent();
    }

    // ---- Variants ------------------------------------------------------------------

    [HttpPost("products/{id:int}/variants")]
    public async Task<IActionResult> CreateVariant(int id, VariantRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _variants.CreateAsync(id, request, ct));

    [HttpPut("variants/{id:int}")]
    public async Task<ActionResult<VariantAdminDto>> UpdateVariant(int id, VariantRequest request, CancellationToken ct) =>
        await _variants.UpdateAsync(id, request, ct);

    [HttpPatch("variants/{id:int}/visibility")]
    public async Task<IActionResult> SetVariantVisibility(int id, SetActiveRequest request, CancellationToken ct)
    {
        await _variants.SetVisibilityAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ---- Images --------------------------------------------------------------------

    [HttpPost("products/{id:int}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var uploads = files.Select(f => new FileUpload(f.FileName, f.Length, f.OpenReadStream)).ToList();
        return StatusCode(StatusCodes.Status201Created, await _images.UploadAsync(id, uploads, ct));
    }

    [HttpPatch("images/{id:int}/thumbnail")]
    public async Task<IActionResult> SetThumbnail(int id, CancellationToken ct)
    {
        await _images.SetThumbnailAsync(id, ct);
        return NoContent();
    }

    [HttpPut("products/{id:int}/images/order")]
    public async Task<IActionResult> ReorderImages(int id, ImageOrderRequest request, CancellationToken ct)
    {
        await _images.ReorderAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("images/{id:int}/variant")]
    public async Task<IActionResult> SetImageVariant(int id, ImageVariantRequest request, CancellationToken ct)
    {
        await _images.SetVariantAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("images/{id:int}")]
    public async Task<IActionResult> DeleteImage(int id, CancellationToken ct)
    {
        await _images.DeleteAsync(id, ct);
        return NoContent();
    }
}
