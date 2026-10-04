using System.Net;
using Daisy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class CartTests
{
    private readonly DaisyApiFactory _factory;

    public CartTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cart_with_an_invalid_or_expired_token_returns_401_so_the_client_can_refresh()
    {
        // API_DESIGN §3.2 / §3.4: a Bearer token takes precedence on cart endpoints; when it is no longer
        // valid the API must answer 401 instead of silently treating the user as a guest.
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var client = new ApiClient(_factory.CreateClient(), token: "expired.or.invalid.token", cartSession: Guid.NewGuid().ToString("N"));

        var get = await client.ExpectAsync(HttpStatusCode.Unauthorized, client.GetAsync("/api/cart"));
        Assert.Equal("UNAUTHORIZED", get!["code"]!.GetValue<string>());

        var add = await client.ExpectAsync(HttpStatusCode.Unauthorized, client.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));
        Assert.Equal("UNAUTHORIZED", add!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Merging_an_out_of_stock_variant_keeps_the_users_own_cart_line()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 3);
        var (customer, email, _) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 2 }));

        var session = Guid.NewGuid().ToString("N");
        var guest = new ApiClient(_factory.CreateClient(), cartSession: session);
        await guest.ExpectAsync(HttpStatusCode.OK, guest.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));

        await _factory.WithDbAsync(db => db.ProductVariants
            .Where(v => v.VariantId == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 0)));

        var token = await _factory.LoginAsync(email, "Secret123", cartSession: session);
        var signedIn = new ApiClient(_factory.CreateClient(), token);
        var cart = await signedIn.ExpectAsync(HttpStatusCode.OK, signedIn.GetAsync("/api/cart"));

        // The user's line stays, shown as unavailable (BR-10, UC-27 A1); the guest line is gone (BR-22).
        var line = Assert.Single(cart!["items"]!.AsArray());
        Assert.Equal(2, line!["quantity"]!.GetValue<int>());
        Assert.Equal("OutOfStock", line["unavailableReason"]!.GetValue<string>());

        var guestCart = await guest.ExpectAsync(HttpStatusCode.OK, guest.GetAsync("/api/cart"));
        Assert.Empty(guestCart!["items"]!.AsArray());
    }

    [Fact]
    public async Task Correlation_id_is_echoed_back()
    {
        // API_DESIGN §2.5: X-Correlation-Id is optional and echoed back in the response.
        var http = _factory.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add("X-Correlation-Id", id);

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(id, Assert.Single(values));
    }
}
