using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-45 – UC-50: products, variants, images, featured and flash sale.</summary>
public sealed class ProductsController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index([FromQuery] AdminProductFilter filter)
    {
        ViewData["Title"] = "Sản phẩm";
        return View(new ProductsAdminViewModel(filter, await admin.GetProductsAsync(filter.ToQuery()), await admin.GetCategoriesAsync()));
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Thêm sản phẩm";
        return View("Edit", await BuildAsync(null, new ProductForm()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] ProductForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var product = await admin.CreateProductAsync(form.ToRequest());
                Flash("Đã tạo sản phẩm. Thêm biến thể và ảnh để bắt đầu bán.");
                return RedirectToAction(nameof(Edit), new { id = product.Id });
            }
            catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
            {
                AddApiError(ex, "Form");
            }
        }

        ViewData["Title"] = "Thêm sản phẩm";
        return View("Edit", await BuildAsync(null, form));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await admin.GetProductAsync(id);
        ViewData["Title"] = product.Name;
        return View(await BuildAsync(product, ProductForm.From(product)));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] ProductForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await admin.UpdateProductAsync(id, form.ToRequest());
                Flash("Đã lưu thông tin sản phẩm.");
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
            {
                AddApiError(ex, "Form");
            }
        }

        var product = await admin.GetProductAsync(id);
        ViewData["Title"] = product.Name;
        return View(await BuildAsync(product, form));
    }

    [HttpPost]
    public Task<IActionResult> Visibility(int id, bool isActive) =>
        RunAsync(() => admin.SetProductVisibilityAsync(id, isActive), isActive ? "Sản phẩm đã hiện trên cửa hàng." : "Đã ẩn sản phẩm. Đơn hàng cũ không bị ảnh hưởng.", $"/admin/products/edit/{id}");

    [HttpPost]
    public Task<IActionResult> Featured(int id, bool isFeatured) =>
        RunAsync(() => admin.SetProductFeaturedAsync(id, isFeatured), isFeatured ? "Đã đưa vào mục Nổi bật." : "Đã bỏ khỏi mục Nổi bật.", $"/admin/products/edit/{id}");

    /// <summary>UC-50 (Admin only).</summary>
    [HttpPost]
    public async Task<IActionResult> Sale(int id, SaleForm sale, bool clear = false)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        if (clear)
        {
            return await RunAsync(() => admin.ClearSaleAsync(id), "Đã bỏ giá khuyến mãi.", $"/admin/products/edit/{id}#sale");
        }

        return await RunAsync(
            () => admin.SetSaleAsync(id, sale.SalePrice, Display.FromInput(sale.StartAt), Display.FromInput(sale.EndAt)),
            "Đã lưu giá khuyến mãi.",
            $"/admin/products/edit/{id}#sale");
    }

    // ---- Variants (UC-47) --------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> AddVariant(int id, [Bind(Prefix = "NewVariant")] VariantForm form)
    {
        if (!ModelState.IsValid)
        {
            Flash(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage, "bad");
            return Redirect($"/admin/products/edit/{id}#variants");
        }

        return await RunAsync(() => admin.CreateVariantAsync(id, form.ToCreateRequest()), "Đã thêm biến thể.", $"/admin/products/edit/{id}#variants");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateVariant(int id, int variantId, VariantForm form)
    {
        if (!ModelState.IsValid)
        {
            Flash(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage, "bad");
            return Redirect($"/admin/products/edit/{id}#variants");
        }

        return await RunAsync(() => admin.UpdateVariantAsync(variantId, form.ToUpdateRequest()), "Đã lưu biến thể.", $"/admin/products/edit/{id}#variants");
    }

    [HttpPost]
    public Task<IActionResult> VariantVisibility(int id, int variantId, bool isActive) =>
        RunAsync(() => admin.SetVariantVisibilityAsync(variantId, isActive), isActive ? "Đã mở bán lại biến thể." : "Đã ẩn biến thể.", $"/admin/products/edit/{id}#variants");

    // ---- Images (UC-48) -------------------------------------------------------------------------

    [HttpPost]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile> files)
    {
        if (files.Count == 0)
        {
            Flash("Chọn ít nhất một ảnh.", "bad");
            return Redirect($"/admin/products/edit/{id}#images");
        }

        return await RunAsync(() => admin.UploadImagesAsync(id, files), $"Đã tải lên {files.Count} ảnh.", $"/admin/products/edit/{id}#images");
    }

    [HttpPost]
    public Task<IActionResult> Thumbnail(int id, int imageId) =>
        RunAsync(() => admin.SetThumbnailAsync(imageId), "Đã chọn ảnh đại diện.", $"/admin/products/edit/{id}#images");

    [HttpPost]
    public Task<IActionResult> ImageVariant(int id, int imageId, int? variantId) =>
        RunAsync(() => admin.SetImageVariantAsync(imageId, variantId), "Đã gắn ảnh với biến thể.", $"/admin/products/edit/{id}#images");

    [HttpPost]
    public Task<IActionResult> DeleteImage(int id, int imageId) =>
        RunAsync(() => admin.DeleteImageAsync(imageId), "Đã xóa ảnh.", $"/admin/products/edit/{id}#images");

    /// <summary>Moves an image one place earlier or later in the gallery.</summary>
    [HttpPost]
    public async Task<IActionResult> MoveImage(int id, int imageId, int direction)
    {
        var product = await admin.GetProductAsync(id);
        var ids = product.Images.OrderBy(i => i.SortOrder).Select(i => i.Id).ToList();
        var index = ids.IndexOf(imageId);
        var target = index + Math.Sign(direction);
        if (index >= 0 && target >= 0 && target < ids.Count)
        {
            (ids[index], ids[target]) = (ids[target], ids[index]);
            return await RunAsync(() => admin.ReorderImagesAsync(id, ids), "Đã đổi thứ tự ảnh.", $"/admin/products/edit/{id}#images");
        }

        return Redirect($"/admin/products/edit/{id}#images");
    }

    private async Task<ProductEditViewModel> BuildAsync(Models.Api.ProductAdmin? product, ProductForm form)
    {
        var sale = new SaleForm
        {
            SalePrice = product?.SalePrice ?? 0,
            StartAt = product?.SaleStartAt is { } s ? Display.InVietnam(s).DateTime : null,
            EndAt = product?.SaleEndAt is { } e ? Display.InVietnam(e).DateTime : null,
        };
        var variant = new VariantForm { Sku = product is null ? string.Empty : product.Slug.ToUpperInvariant()[..Math.Min(12, product.Slug.Length)] + "-" };
        return new ProductEditViewModel(product, form, variant, sale, await admin.GetCategoriesAsync(), IsAdmin);
    }
}
