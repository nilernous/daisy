using System.Net;
using Daisy.Tests.Infrastructure;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class CatalogTests
{
    private readonly DaisyApiFactory _factory;

    public CatalogTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Product_detail_applies_the_flash_sale_and_variant_extra_price()
    {
        var client = new ApiClient(_factory.CreateClient());
        var product = await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync("/api/products/ao-thun-cotton-basic"));

        Assert.True(product!["isOnSale"]!.GetValue<bool>());
        Assert.Equal(149000m, product["effectivePrice"]!.GetValue<decimal>());
        var variants = product["variants"]!.AsArray();
        Assert.Contains(variants, v => v!["size"]!.GetValue<string>() == "M" && v["price"]!.GetValue<decimal>() == 149000m);
        Assert.Contains(variants, v => v!["size"]!.GetValue<string>() == "XL" && v["price"]!.GetValue<decimal>() == 169000m);
    }

    [Fact]
    public async Task OData_filters_by_variant_and_price_and_rejects_expand()
    {
        var client = new ApiClient(_factory.CreateClient());
        var filter = Uri.EscapeDataString("variants/any(v: v/size eq 'XL') and effectivePrice le 300000");
        var list = await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync($"/odata/products?$filter={filter}&$orderby=effectivePrice&$count=true"));
        var items = list!["value"]!.AsArray();
        Assert.NotEmpty(items);
        Assert.All(items, p => Assert.True(p!["effectivePrice"]!.GetValue<decimal>() <= 300000m));

        var search = Uri.EscapeDataString("contains(tolower(name),'áo thun')");
        var found = await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync($"/odata/products?$filter={search}&$count=true"));
        Assert.True(found!["@odata.count"]!.GetValue<int>() >= 2);

        var bad = await client.ExpectAsync(HttpStatusCode.BadRequest, client.GetAsync("/odata/products?$expand=variants"));
        Assert.Equal("VALIDATION_FAILED", bad!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Hiding_a_parent_category_hides_its_products()
    {
        var (productId, _) = await _factory.NewProductAsync(120000, 5, categoryId: 11);
        var client = new ApiClient(_factory.CreateClient());
        var staff = await _factory.AsStaffAsync();
        string slug = (await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync($"/api/admin/products/{productId}")))!["slug"]!.GetValue<string>();

        await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync($"/api/products/{slug}"));

        // Category 3 "Phụ kiện" is the parent of 11 "Mũ nón" (BR-10, BR-13).
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync("/api/admin/categories/3/visibility", new { isActive = false }));
        try
        {
            await client.ExpectAsync(HttpStatusCode.NotFound, client.GetAsync($"/api/products/{slug}"));
            var list = await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync($"/odata/products?$filter=id eq {productId}"));
            Assert.Empty(list!["value"]!.AsArray());
            var tree = await client.ExpectAsync(HttpStatusCode.OK, client.GetAsync("/api/categories"));
            Assert.DoesNotContain(tree!.AsArray(), c => c!["id"]!.GetValue<int>() == 3);
        }
        finally
        {
            await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync("/api/admin/categories/3/visibility", new { isActive = true }));
        }
    }

    [Fact]
    public async Task A_category_cannot_be_moved_under_its_own_child()
    {
        var staff = await _factory.AsStaffAsync();
        var body = await staff.ExpectAsync(HttpStatusCode.BadRequest, staff.PutAsync("/api/admin/categories/1", new
        {
            name = "Nam",
            slug = "nam",
            parentCategoryId = 4,
            sortOrder = 0,
        }));
        Assert.Equal("INVALID_CATEGORY_PARENT", body!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Duplicate_sku_and_size_color_are_rejected()
    {
        var (productId, _) = await _factory.NewProductAsync(100000, 3);
        var staff = await _factory.AsStaffAsync();
        var duplicateOption = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PostAsync($"/api/admin/products/{productId}/variants", new
        {
            sku = $"NEW-{Guid.NewGuid():N}"[..20],
            size = "M",
            color = "Đen",
            extraPrice = 0,
            stockQuantity = 1,
            lowStockThreshold = 1,
        }));
        Assert.Equal("VARIANT_OPTION_EXISTS", duplicateOption!["code"]!.GetValue<string>());

        var duplicateSku = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PostAsync($"/api/admin/products/{productId}/variants", new
        {
            sku = "ts-basic-m-wht",
            size = "L",
            color = "Đen",
            extraPrice = 0,
            lowStockThreshold = 1,
        }));
        Assert.Equal("SKU_ALREADY_EXISTS", duplicateSku!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Stock_update_is_rejected_when_stock_changed_meanwhile()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 7);
        var staff = await _factory.AsStaffAsync();

        var stale = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PutAsync($"/api/admin/variants/{variantId}/stock", new { stockQuantity = 50, expectedCurrentStock = 6 }));
        Assert.Equal(7, stale!["details"]!["currentStock"]!.GetValue<int>());

        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PutAsync($"/api/admin/variants/{variantId}/stock", new { stockQuantity = 50, expectedCurrentStock = 7 }));
        Assert.Equal(50, await _factory.StockAsync(variantId));
    }
}
