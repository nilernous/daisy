using System.Net;
using Daisy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class OrderTests
{
    private readonly DaisyApiFactory _factory;

    public OrderTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_moves_one_step_at_a_time_and_completed_is_final()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "COD");
        var id = placed["orderId"]!.GetValue<int>();
        var staff = await _factory.AsStaffAsync();

        var skip = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PatchAsync($"/api/admin/orders/{id}/status", new { status = "Shipping" }));
        Assert.Equal("INVALID_STATUS_TRANSITION", skip!["code"]!.GetValue<string>());

        foreach (var status in new[] { "Confirmed", "Shipping", "Completed" })
        {
            await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/orders/{id}/status", new { status }));
        }

        var detail = await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync($"/api/admin/orders/{id}"));
        Assert.NotNull(detail!["completedAt"]);
        Assert.Empty(detail["allowedStatusTransitions"]!.AsArray());
        await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PostAsync($"/api/admin/orders/{id}/cancel", new { reason = "Quá muộn" }));

        // COD collection confirmed by staff, then refunded (BR-34).
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/orders/{id}/payment", new { action = "ConfirmCod" }));
        await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PatchAsync($"/api/admin/orders/{id}/payment", new { action = "ConfirmCod" }));
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/orders/{id}/payment", new { action = "Refund" }));
    }

    [Fact]
    public async Task Canceling_restores_stock_sold_count_and_voucher_use()
    {
        var (productId, variantId) = await _factory.NewProductAsync(400000, 6);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        int usedBefore = await _factory.WithScopeAsync(sp => sp.GetDb().Vouchers.Where(v => v.Code == "DAISY10").Select(v => v.UsedCount).FirstAsync());
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 3, "COD", "DAISY10");
        Assert.Equal(3, await _factory.StockAsync(variantId));

        var missingReason = await customer.ExpectAsync(HttpStatusCode.BadRequest, customer.PostAsync($"/api/orders/{placed["orderId"]}/cancel", new { reason = " " }));
        Assert.Equal("VALIDATION_FAILED", missingReason!["code"]!.GetValue<string>());

        var canceled = await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync($"/api/orders/{placed["orderId"]}/cancel", new { reason = "Đặt nhầm size" }));
        Assert.Equal("Canceled", canceled!["orderStatus"]!.GetValue<string>());
        Assert.False(canceled["refundRequired"]!.GetValue<bool>());

        Assert.Equal(6, await _factory.StockAsync(variantId));
        Assert.Equal(0, await _factory.WithScopeAsync(sp => sp.GetDb().Products.Where(p => p.ProductId == productId).Select(p => p.SoldCount).FirstAsync()));
        Assert.Equal(usedBefore, await _factory.WithScopeAsync(sp => sp.GetDb().Vouchers.Where(v => v.Code == "DAISY10").Select(v => v.UsedCount).FirstAsync()));

        var twice = await customer.ExpectAsync(HttpStatusCode.Conflict, customer.PostAsync($"/api/orders/{placed["orderId"]}/cancel", new { reason = "Lần nữa" }));
        Assert.Equal("ORDER_NOT_CANCELABLE", twice!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Customers_cannot_see_or_cancel_other_customers_orders()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var (alice, _, addressId) = await _factory.NewCustomerAsync();
        var (bob, _, _) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(alice, addressId, variantId, 1, "COD");

        await bob.ExpectAsync(HttpStatusCode.NotFound, bob.GetAsync($"/api/orders/{placed["orderId"]}"));
        await bob.ExpectAsync(HttpStatusCode.NotFound, bob.PostAsync($"/api/orders/{placed["orderId"]}/cancel", new { reason = "x" }));
        await bob.ExpectAsync(HttpStatusCode.NotFound, bob.PutAsync($"/api/profile/addresses/{addressId}", new
        {
            receiverName = "x",
            receiverPhone = "0901234567",
            province = "x",
            district = "x",
            ward = "x",
            streetAddress = "x",
        }));
    }

    [Fact]
    public async Task Staff_can_filter_orders_with_OData()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "COD");
        var code = placed["orderCode"]!.GetValue<string>();
        var staff = await _factory.AsStaffAsync();

        var filter = Uri.EscapeDataString($"orderStatus eq 'Pending' and contains(orderCode,'{code}') and createdAt ge 2020-01-01T00:00:00+07:00");
        var list = await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync($"/odata/orders?$filter={filter}&$count=true"));
        Assert.Equal(1, list!["@odata.count"]!.GetValue<int>());
        Assert.Equal("Khách Thử Nghiệm", list["value"]![0]!["customerName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dashboard_returns_every_status_and_a_point_per_day()
    {
        var admin = await _factory.AsAdminAsync();
        var byStatus = await admin.ExpectAsync(HttpStatusCode.OK, admin.GetAsync("/api/admin/dashboard/orders-by-status"));
        Assert.Equal(5, byStatus!.AsArray().Count);

        var revenue = await admin.ExpectAsync(HttpStatusCode.OK, admin.GetAsync("/api/admin/dashboard/revenue?groupBy=day"));
        Assert.Equal(30, revenue!.AsArray().Count);
        Assert.True(revenue.AsArray().Sum(p => p!["revenue"]!.GetValue<decimal>()) >= 428000m + 648000m);

        var months = await admin.ExpectAsync(HttpStatusCode.OK, admin.GetAsync("/api/admin/dashboard/new-customers?groupBy=month&from=2026-01-01&to=2026-12-31"));
        Assert.Equal(12, months!.AsArray().Count);

        await admin.ExpectAsync(HttpStatusCode.BadRequest, admin.GetAsync("/api/admin/dashboard/revenue?groupBy=week"));
    }
}
