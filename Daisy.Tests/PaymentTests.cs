using System.Net;
using System.Text;
using System.Text.Json;
using Daisy.Application.Features.Orders;
using Daisy.Domain.Enums;
using Daisy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class PaymentTests
{
    private readonly DaisyApiFactory _factory;

    public PaymentTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_delivered_twice_pays_the_order_once()
    {
        var (_, variantId) = await _factory.NewProductAsync(200000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "Online");
        var code = placed["orderCode"]!.GetValue<string>();
        var total = placed["totalAmount"]!.GetValue<decimal>();

        var qr = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{placed["orderId"]}/payment-qr"));
        Assert.Equal(code, qr!["transferContent"]!.GetValue<string>());
        Assert.NotNull(qr["expiresAt"]);

        var reference = $"FT{Guid.NewGuid():N}"[..20];
        var payload = Webhook(reference, total, $"chuyen khoan {code.ToLowerInvariant()}");
        Assert.Equal(HttpStatusCode.OK, await PostWebhookAsync(payload));
        Assert.Equal(HttpStatusCode.OK, await PostWebhookAsync(payload));

        var order = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{placed["orderId"]}"));
        Assert.Equal("Paid", order!["paymentStatus"]!.GetValue<string>());
        var rows = await _factory.WithScopeAsync(sp => sp.GetDb().PaymentTransactions.CountAsync(t => t.BankReference == reference));
        Assert.Equal(1, rows);
        await customer.ExpectAsync(HttpStatusCode.Conflict, customer.GetAsync($"/api/orders/{placed["orderId"]}/payment-qr"));
    }

    [Fact]
    public async Task Webhook_with_a_wrong_key_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(Webhook("X1", 1000, "x"), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Apikey wrong-key");
        var response = await _factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Underpaid_and_unmatched_transfers_wait_for_staff_who_can_apply_them()
    {
        var (_, variantId) = await _factory.NewProductAsync(300000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "Online");
        var orderId = placed["orderId"]!.GetValue<int>();
        var total = placed["totalAmount"]!.GetValue<decimal>();
        var code = placed["orderCode"]!.GetValue<string>();

        var underpaidRef = $"UP{Guid.NewGuid():N}"[..20];
        await PostWebhookAsync(Webhook(underpaidRef, total - 1000, code));
        var wrongContentRef = $"WC{Guid.NewGuid():N}"[..20];
        await PostWebhookAsync(Webhook(wrongContentRef, total, "mua ao thun"));

        var staff = await _factory.AsStaffAsync();
        var list = await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync("/api/admin/payment-transactions?needsReview=true&pageSize=100"));
        var items = list!["items"]!.AsArray();
        var underpaid = items.Single(t => t!["bankReference"]!.GetValue<string>() == underpaidRef)!;
        var unmatched = items.Single(t => t!["bankReference"]!.GetValue<string>() == wrongContentRef)!;
        Assert.Equal("Underpaid", underpaid["matchResult"]!.GetValue<string>());
        Assert.Equal(orderId, underpaid["order"]!["id"]!.GetValue<int>());
        Assert.Equal("NoOrder", unmatched["matchResult"]!.GetValue<string>());

        // Underpaid transfer cannot pay the order; it is refunded.
        var notPayable = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PostAsync($"/api/admin/payment-transactions/{underpaid["id"]}/apply", new { orderId }));
        Assert.Equal("ORDER_NOT_PAYABLE", notPayable!["code"]!.GetValue<string>());
        await staff.ExpectAsync(HttpStatusCode.OK, staff.PatchAsync($"/api/admin/payment-transactions/{underpaid["id"]}/resolution", new { resolution = "Refunded", note = "Đã hoàn tiền cho khách" }));

        // The full transfer with the wrong content is applied to the order.
        var applied = await staff.ExpectAsync(HttpStatusCode.OK, staff.PostAsync($"/api/admin/payment-transactions/{unmatched["id"]}/apply", new { orderId, note = "Khách ghi sai nội dung" }));
        Assert.Equal("Applied", applied!["resolution"]!.GetValue<string>());
        Assert.Equal("Nhân viên Daisy", applied["resolvedBy"]!["fullName"]!.GetValue<string>());

        var order = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{orderId}"));
        Assert.Equal("Paid", order!["paymentStatus"]!.GetValue<string>());

        var again = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PatchAsync($"/api/admin/payment-transactions/{unmatched["id"]}/resolution", new { resolution = "Ignored", note = "x" }));
        Assert.Equal("TRANSACTION_ALREADY_RESOLVED", again!["code"]!.GetValue<string>());

        // A bank reference already recorded cannot be used for a manual confirmation (BR-42).
        var (_, otherVariant) = await _factory.NewProductAsync(100000, 5);
        var other = await TestData.PlaceOrderAsync(customer, addressId, otherVariant, 1, "Online");
        var recorded = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PatchAsync($"/api/admin/orders/{other["orderId"]}/payment", new { action = "ConfirmTransfer", transactionCode = wrongContentRef }));
        Assert.Equal("TRANSACTION_RECORDED", recorded!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Unpaid_order_expires_releases_stock_and_a_late_transfer_is_not_matched()
    {
        var (_, variantId) = await _factory.NewProductAsync(150000, 4);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var placed = await TestData.PlaceOrderAsync(customer, addressId, variantId, 2, "Online", voucherCode: null);
        var orderId = placed["orderId"]!.GetValue<int>();
        Assert.Equal(2, await _factory.StockAsync(variantId));

        // Inside the grace period nothing happens.
        _factory.Clock.Offset = TimeSpan.FromMinutes(33);
        try
        {
            Assert.Equal(0, await RunExpiryAsync());

            // Past deadline (30 min) + grace (5 min).
            _factory.Clock.Offset = TimeSpan.FromMinutes(36);
            Assert.Equal(1, await RunExpiryAsync());
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }

        var order = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{orderId}"));
        Assert.Equal("Canceled", order!["orderStatus"]!.GetValue<string>());
        Assert.Equal("Payment not received in time", order["cancelReason"]!.GetValue<string>());
        Assert.Equal(4, await _factory.StockAsync(variantId));

        var reference = $"LATE{Guid.NewGuid():N}"[..20];
        await PostWebhookAsync(Webhook(reference, placed["totalAmount"]!.GetValue<decimal>(), placed["orderCode"]!.GetValue<string>()));
        var match = await _factory.WithScopeAsync(sp => sp.GetDb().PaymentTransactions.Where(t => t.BankReference == reference).Select(t => t.MatchResult).FirstAsync());
        Assert.Equal(PaymentMatchResult.OrderNotPayable, match);
        order = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync($"/api/orders/{orderId}"));
        Assert.Equal("Unpaid", order!["paymentStatus"]!.GetValue<string>());
    }

    [Fact]
    public async Task Unpaid_VietQR_order_cannot_be_confirmed_but_COD_can()
    {
        var (_, variantId) = await _factory.NewProductAsync(100000, 5);
        var (customer, _, addressId) = await _factory.NewCustomerAsync();
        var online = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "Online");
        var cod = await TestData.PlaceOrderAsync(customer, addressId, variantId, 1, "COD");
        var staff = await _factory.AsStaffAsync();

        var blocked = await staff.ExpectAsync(HttpStatusCode.Conflict, staff.PatchAsync($"/api/admin/orders/{online["orderId"]}/status", new { status = "Confirmed" }));
        Assert.Equal("ORDER_NOT_PAID", blocked!["code"]!.GetValue<string>());
        await staff.ExpectAsync(HttpStatusCode.NoContent, staff.PatchAsync($"/api/admin/orders/{cod["orderId"]}/status", new { status = "Confirmed" }));
    }

    private async Task<int> RunExpiryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OrderExpiryService>().CancelExpiredOrdersAsync(CancellationToken.None);
    }

    private async Task<HttpStatusCode> PostWebhookAsync(string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Apikey {DaisyApiFactory.WebhookKey}");
        var response = await _factory.CreateClient().SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>A SePay-format webhook body (API_DESIGN §6.8).</summary>
    private static string Webhook(string reference, decimal amount, string content) =>
        JsonSerializer.Serialize(new
        {
            id = Random.Shared.NextInt64(1, long.MaxValue),
            gateway = "Vietcombank",
            transactionDate = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
            accountNumber = "0123456789",
            content,
            transferType = "in",
            transferAmount = amount,
            referenceCode = reference,
        });
}
