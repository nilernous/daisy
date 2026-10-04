using System.Net;
using Daisy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class CheckoutTests
{
    private readonly DaisyApiFactory _factory;

    public CheckoutTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_cart_is_merged_into_the_user_cart_on_login_capped_at_stock()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 3);
        var (customer, email, _) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 2 }));

        var session = Guid.NewGuid().ToString("N");
        var guest = new ApiClient(_factory.CreateClient(), cartSession: session);
        await guest.ExpectAsync(HttpStatusCode.OK, guest.PostAsync("/api/cart/items", new { variantId, quantity = 2 }));

        var token = await _factory.LoginAsync(email, "Secret123", cartSession: session);
        var signedIn = new ApiClient(_factory.CreateClient(), token);
        var cart = await signedIn.ExpectAsync(HttpStatusCode.OK, signedIn.GetAsync("/api/cart"));
        var line = Assert.Single(cart!["items"]!.AsArray());
        Assert.Equal(3, line!["quantity"]!.GetValue<int>()); // 2 + 2 capped at stock 3 (BR-22, BR-20)

        var guestCart = await guest.ExpectAsync(HttpStatusCode.OK, guest.GetAsync("/api/cart"));
        Assert.Empty(guestCart!["items"]!.AsArray());
    }

    [Fact]
    public async Task Voucher_preview_follows_the_discount_rules()
    {
        var (_, variantId) = await _factory.NewProductAsync(600000, 5);
        var (customer, _, _) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));

        // DAISY10: 10% of 600.000 = 60.000, capped at 50.000; shipping not discounted (BR-27).
        var totals = await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/checkout/voucher", new { code = "daisy10" }));
        Assert.Equal(50000m, totals!["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(600000m - 50000m + 30000m, totals["totalAmount"]!.GetValue<decimal>());

        var missing = await customer.ExpectAsync(HttpStatusCode.NotFound, customer.PostAsync("/api/checkout/voucher", new { code = "NOPE" }));
        Assert.Equal("VOUCHER_NOT_FOUND", missing!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ineligible_voucher_returns_the_reason()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, _) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));

        var body = await customer.ExpectAsync(HttpStatusCode.BadRequest, customer.PostAsync("/api/checkout/voucher", new { code = "WELCOME50K" }));
        Assert.Equal("VOUCHER_NOT_ELIGIBLE", body!["code"]!.GetValue<string>());
        Assert.Equal("BelowMinimumOrder", body["details"]!["reason"]!.GetValue<string>());
    }

    [Fact]
    public async Task Placing_an_order_is_atomic_and_snapshots_the_order()
    {
        var (productId, variantId) = await _factory.NewProductAsync(250000, 10);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        int usedBefore = await _factory.WithScopeAsync(sp => sp.GetDb().Vouchers.Where(v => v.Code == "DAISY10").Select(v => v.UsedCount).FirstAsync());

        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 2 }));
        var wrong = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync("/api/checkout", new
        {
            addressId,
            paymentMethod = "COD",
            voucherCode = "DAISY10",
            expectedTotal = 1,
        }));
        Assert.Equal("TOTAL_CHANGED", wrong!["code"]!.GetValue<string>());
        var newTotal = wrong["details"]!["newTotal"]!.GetValue<decimal>();
        Assert.Equal(500000m - 50000m + 30000m, newTotal);

        var placed = await customer.ExpectAsync(HttpStatusCode.Created, customer.PostAsync("/api/checkout", new
        {
            addressId,
            paymentMethod = "COD",
            voucherCode = "DAISY10",
            expectedTotal = newTotal,
        }));
        Assert.Matches("^DS[0-9]{6}[A-Z0-9]{4}$", placed!["orderCode"]!.GetValue<string>());
        Assert.Null(placed["paymentQrUrl"]);

        Assert.Equal(8, await _factory.StockAsync(variantId));
        var sold = await _factory.WithScopeAsync(sp => sp.GetDb().Products.Where(p => p.ProductId == productId).Select(p => p.SoldCount).FirstAsync());
        Assert.Equal(2, sold);
        int usedAfter = await _factory.WithScopeAsync(sp => sp.GetDb().Vouchers.Where(v => v.Code == "DAISY10").Select(v => v.UsedCount).FirstAsync());
        Assert.Equal(usedBefore + 1, usedAfter);

        var cart = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync("/api/cart"));
        Assert.Empty(cart!["items"]!.AsArray());

        // Repeated submit cannot create a second order.
        var again = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync("/api/checkout", new
        {
            addressId,
            paymentMethod = "COD",
            expectedTotal = newTotal,
        }));
        Assert.Equal("NO_ITEMS_SELECTED", again!["code"]!.GetValue<string>());

        var detail = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{placed["orderId"]}"));
        Assert.Equal("1 Võ Văn Tần, Phường 7, Quận 3, TP. Hồ Chí Minh", detail!["shippingAddress"]!.GetValue<string>());
        Assert.Equal(250000m, detail["items"]![0]!["unitPrice"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Two_customers_cannot_both_buy_the_last_item()
    {
        var (_, variantId) = await _factory.NewProductAsync(90000, 1);
        var (alice, _, aliceAddress) = await _factory.NewCustomerAsync();
        var (bob, _, bobAddress) = await _factory.NewCustomerAsync();
        await alice.ExpectAsync(HttpStatusCode.OK, alice.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));
        await bob.ExpectAsync(HttpStatusCode.OK, bob.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));

        var results = await Task.WhenAll(
            alice.PostAsync("/api/checkout", new { addressId = aliceAddress, paymentMethod = "COD", expectedTotal = 120000 }),
            bob.PostAsync("/api/checkout", new { addressId = bobAddress, paymentMethod = "COD", expectedTotal = 120000 }));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Conflict));
        Assert.Equal(0, await _factory.StockAsync(variantId));
    }

    [Fact]
    public async Task Hidden_product_cannot_be_checked_out()
    {
        var (productId, variantId) = await _factory.NewProductAsync(80000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity = 1 }));

        var staff = await _factory.AsStaffAsync();
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/products/{productId}/visibility", new { isActive = false }));

        var cart = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync("/api/cart"));
        Assert.Equal("Hidden", cart!["items"]![0]!["unavailableReason"]!.GetValue<string>());
        var body = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync("/api/checkout", new { addressId, paymentMethod = "COD", expectedTotal = 110000 }));
        Assert.Equal("ITEM_UNAVAILABLE", body!["code"]!.GetValue<string>());
    }
}
