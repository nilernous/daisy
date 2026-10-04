using System.Net;
using Daisy.Tests.Infrastructure;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class ReviewTests
{
    private readonly DaisyApiFactory _factory;

    public ReviewTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reviews_need_a_completed_order_and_hidden_reviews_do_not_count()
    {
        var (productId, variantId) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "COD");
        var orderId = placed["orderId"]!.GetValue<int>();
        var order = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{orderId}"));
        var itemId = order!["items"]![0]!["orderItemId"]!.GetValue<int>();

        var early = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync("/api/reviews", new { orderItemId = itemId, rating = 5 }));
        Assert.Equal("REVIEW_NOT_ALLOWED", early!["code"]!.GetValue<string>());

        var staff = await _factory.AsStaffAsync();
        foreach (var status in new[] { "Confirmed", "Shipping", "Completed" })
        {
            await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/orders/{orderId}/status", new { status }));
        }

        await customer.ExpectAsync(HttpStatusCode.BadRequest, customer.PostAsync("/api/reviews", new { orderItemId = itemId, rating = 6 }));
        var review = await customer.ExpectAsync(HttpStatusCode.Created, customer.PostAsync("/api/reviews", new { orderItemId = itemId, rating = 4, comment = "Vải đẹp" }));
        Assert.Equal("Khách T. Nghiệm", review!["userFullName"]!.GetValue<string>());
        var twice = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync("/api/reviews", new { orderItemId = itemId, rating = 3 }));
        Assert.Equal("ALREADY_REVIEWED", twice!["code"]!.GetValue<string>());

        var anonymous = new ApiClient(_factory.CreateClient());
        var reviews = await anonymous.ExpectAsync(HttpStatusCode.OK, anonymous.GetAsync($"/api/products/{productId}/reviews"));
        Assert.Equal(4m, reviews!["averageRating"]!.GetValue<decimal>());
        Assert.Equal(1, reviews["reviewCount"]!.GetValue<int>());

        // BR-38 / BR-39: hiding the review removes it from the rating but keeps it for staff.
        var reviewId = review["id"]!.GetValue<int>();
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/reviews/{reviewId}/visibility", new { isVisible = false }));
        reviews = await anonymous.ExpectAsync(HttpStatusCode.OK, anonymous.GetAsync($"/api/products/{productId}/reviews"));
        Assert.Equal(0m, reviews!["averageRating"]!.GetValue<decimal>());
        Assert.Empty(reviews["reviews"]!["items"]!.AsArray());

        var forStaff = await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync($"/odata/reviews?$filter=productId eq {productId}"));
        Assert.False(forStaff!["value"]![0]!["isVisible"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Wishlist_add_is_idempotent()
    {
        var (productId, _) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, _) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.NoContent, customer.PostAsync($"/api/wishlist/{productId}"));
        await customer.ExpectAsync(HttpStatusCode.NoContent, customer.PostAsync($"/api/wishlist/{productId}"));
        var list = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync("/api/wishlist"));
        Assert.Single(list!.AsArray());
        await customer.ExpectAsync(HttpStatusCode.NoContent, customer.DeleteAsync($"/api/wishlist/{productId}"));
        await customer.ExpectAsync(HttpStatusCode.NoContent, customer.DeleteAsync($"/api/wishlist/{productId}"));
    }
}
