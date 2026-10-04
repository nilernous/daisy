using System.Net;
using System.Text.Json.Nodes;
using Daisy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Tests.Infrastructure;

/// <summary>Creates isolated users and products so tests do not depend on each other.</summary>
public static class TestData
{
    public const string AdminEmail = "admin@daisy.local";
    public const string AdminPassword = "Admin@123";
    public const string StaffEmail = "staff@daisy.local";
    public const string StaffPassword = "Staff@123";

    public static async Task<string> LoginAsync(this DaisyApiFactory factory, string email, string password, string? cartSession = null)
    {
        var client = new ApiClient(factory.CreateClient(), cartSession: cartSession);
        var body = await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/login", new { email, password }));
        return body!["accessToken"]!.GetValue<string>();
    }

    public static async Task<ApiClient> AsAsync(this DaisyApiFactory factory, string email, string password) =>
        new(factory.CreateClient(), await factory.LoginAsync(email, password));

    public static Task<ApiClient> AsAdminAsync(this DaisyApiFactory factory) => factory.AsAsync(AdminEmail, AdminPassword);

    public static Task<ApiClient> AsStaffAsync(this DaisyApiFactory factory) => factory.AsAsync(StaffEmail, StaffPassword);

    /// <summary>Registers a new customer with an address and returns a signed-in client.</summary>
    public static async Task<(ApiClient Client, string Email, int AddressId)> NewCustomerAsync(this DaisyApiFactory factory)
    {
        var email = $"c{Guid.NewGuid():N}@test.local";
        const string password = "Secret123";
        var anonymous = new ApiClient(factory.CreateClient());
        await anonymous.ExpectAsync(HttpStatusCode.Created, anonymous.PostAsync("/api/auth/register", new
        {
            fullName = "Khách Thử Nghiệm",
            email,
            password,
            confirmPassword = password,
        }));

        var client = await factory.AsAsync(email, password);
        var address = await client.ExpectAsync(HttpStatusCode.Created, client.PostAsync("/api/profile/addresses", new
        {
            receiverName = "Khách Thử Nghiệm",
            receiverPhone = "0901234567",
            province = "TP. Hồ Chí Minh",
            district = "Quận 3",
            ward = "Phường 7",
            streetAddress = "1 Võ Văn Tần",
        }));
        return (client, email, address!["id"]!.GetValue<int>());
    }

    /// <summary>Adds a product with one variant directly in the database.</summary>
    public static async Task<(int ProductId, int VariantId)> NewProductAsync(this DaisyApiFactory factory, decimal price, int stock, int categoryId = 4)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var product = new Product
        {
            CategoryId = categoryId,
            ProductName = $"Sản phẩm thử {suffix}",
            Slug = $"san-pham-thu-{suffix}",
            IsActive = true,
            CreatedAt = DateTime.Now,
        };
        product.SetBasePrice(price);
        product.Variants.Add(new ProductVariant
        {
            Sku = $"TEST-{suffix}".ToUpperInvariant(),
            Size = "M",
            Color = "Đen",
            StockQuantity = stock,
            LowStockThreshold = 1,
            IsActive = true,
        });

        await factory.WithDbAsync(async db =>
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        });
        return (product.ProductId, product.Variants.Single().VariantId);
    }

    public static Task<int> StockAsync(this DaisyApiFactory factory, int variantId) =>
        factory.WithScopeAsync(sp => sp.GetDb().ProductVariants.Where(v => v.VariantId == variantId).Select(v => v.StockQuantity).FirstAsync());

    public static Daisy.Infrastructure.Persistence.DaisyDbContext GetDb(this IServiceProvider sp) =>
        (Daisy.Infrastructure.Persistence.DaisyDbContext)sp.GetService(typeof(Daisy.Infrastructure.Persistence.DaisyDbContext))!;

    /// <summary>Adds the variant to the cart and places an order; returns the placed-order body.</summary>
    public static async Task<JsonNode> PlaceOrderAsync(ApiClient customer, int addressId, int variantId, int quantity, string paymentMethod, string? voucherCode = null)
    {
        await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/cart/items", new { variantId, quantity }));
        var preview = voucherCode is null
            ? (await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync("/api/checkout")))!["totals"]!
            : (await customer.ExpectAsync(HttpStatusCode.OK, customer.PostAsync("/api/checkout/voucher", new { code = voucherCode })))!;

        return (await customer.ExpectAsync(HttpStatusCode.Created, customer.PostAsync("/api/checkout", new
        {
            addressId,
            paymentMethod,
            voucherCode,
            expectedTotal = preview["totalAmount"]!.GetValue<decimal>(),
        })))!;
    }
}
