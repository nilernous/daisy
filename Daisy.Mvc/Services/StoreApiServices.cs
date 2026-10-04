using Daisy.Mvc.Models.Api;
using Microsoft.Extensions.Caching.Memory;

namespace Daisy.Mvc.Services;

/// <summary>UC-01 – UC-07.</summary>
public sealed class AuthApiService(ApiClient api)
{
    public Task<UserSummary> RegisterAsync(string fullName, string email, string password, string confirmPassword) =>
        api.PostAsync<UserSummary>("/api/auth/register", new { fullName, email, password, confirmPassword });

    /// <summary>The handler sends X-Cart-Session, so the guest cart is merged (UC-31).</summary>
    public Task<AuthResult> LoginAsync(string email, string password) =>
        api.PostAsync<AuthResult>("/api/auth/login", new { email, password });

    public Task LogoutAsync() => api.PostAsync("/api/auth/logout", null);

    public Task<MessageResult> ForgotPasswordAsync(string email) =>
        api.PostAsync<MessageResult>("/api/auth/forgot-password", new { email });

    public Task ResetPasswordAsync(string token, string newPassword, string confirmPassword) =>
        api.PostAsync("/api/auth/reset-password", new { token, newPassword, confirmPassword });

    public Task ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword) =>
        api.PostAsync("/api/auth/change-password", new { currentPassword, newPassword, confirmPassword });
}

/// <summary>Catalog: categories, OData product lists, product detail, reviews (UC-13 – UC-22, UC-42).</summary>
public sealed class ProductApiService(ApiClient api, IMemoryCache cache)
{
    public async Task<IReadOnlyList<CategoryNode>> GetCategoriesAsync() =>
        await cache.GetOrCreateAsync("categories", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await api.GetAsync<List<CategoryNode>>("/api/categories");
        }) ?? [];

    public Task<ODataPage<ProductListItem>> QueryAsync(ODataQuery query) => api.ODataAsync<ProductListItem>("products", query);

    public Task<ProductDetail> GetDetailAsync(string slug) => api.GetAsync<ProductDetail>($"/api/products/{Uri.EscapeDataString(slug)}");

    public Task<ProductReviews> GetReviewsAsync(int productId, int page, int? rating) =>
        api.GetAsync<ProductReviews>($"/api/products/{productId}/reviews?page={page}&pageSize=10{(rating is null ? "" : $"&rating={rating}")}");
}

/// <summary>UC-26 – UC-30.</summary>
public sealed class CartApiService(ApiClient api)
{
    public Task<Cart> GetAsync() => api.GetAsync<Cart>("/api/cart");

    public Task<CartSummary> AddAsync(int variantId, int quantity) =>
        api.PostAsync<CartSummary>("/api/cart/items", new { variantId, quantity });

    public Task<Cart> UpdateAsync(int itemId, int quantity) => api.PutAsync<Cart>($"/api/cart/items/{itemId}", new { quantity });

    public Task<Cart> RemoveAsync(int itemId) => api.DeleteAsync<Cart>($"/api/cart/items/{itemId}");

    public Task<Cart> SelectAsync(IEnumerable<int> itemIds, bool selected) =>
        api.PutAsync<Cart>("/api/cart/selection", new { itemIds, selected });
}

/// <summary>UC-32 – UC-35, UC-37 – UC-41.</summary>
public sealed class OrderApiService(ApiClient api)
{
    public Task<Checkout> GetCheckoutAsync() => api.GetAsync<Checkout>("/api/checkout");

    public Task<CheckoutTotals> PreviewVoucherAsync(string code) => api.PostAsync<CheckoutTotals>("/api/checkout/voucher", new { code });

    public Task<PlacedOrder> PlaceOrderAsync(int addressId, PaymentMethod paymentMethod, string? voucherCode, string? customerNote, decimal expectedTotal) =>
        api.PostAsync<PlacedOrder>("/api/checkout", new { addressId, paymentMethod, voucherCode, customerNote, expectedTotal });

    public Task<PagedResult<OrderSummary>> GetOrdersAsync(int page, OrderStatus? status) =>
        api.GetAsync<PagedResult<OrderSummary>>($"/api/orders?page={page}&pageSize=10{(status is null ? "" : $"&status={status}")}");

    public Task<OrderDetail> GetOrderAsync(int id) => api.GetAsync<OrderDetail>($"/api/orders/{id}");

    public Task<CanceledOrder> CancelAsync(int id, string reason) => api.PostAsync<CanceledOrder>($"/api/orders/{id}/cancel", new { reason });

    public Task<Review> ReviewAsync(int orderItemId, int rating, string? comment) =>
        api.PostAsync<Review>("/api/reviews", new { orderItemId, rating, comment });
}

/// <summary>UC-35 VietQR.</summary>
public sealed class PaymentApiService(ApiClient api)
{
    public Task<PaymentQr> GetQrAsync(int orderId) => api.GetAsync<PaymentQr>($"/api/orders/{orderId}/payment-qr");

    /// <summary>Development only (Mock provider).</summary>
    public Task SimulateTransferAsync(string orderCode) =>
        api.PostAsync("/api/dev/payments/simulate-transfer", new { orderCode });
}

/// <summary>UC-08 – UC-12, UC-23 – UC-25.</summary>
public sealed class ProfileApiService(ApiClient api)
{
    public Task<Profile> GetProfileAsync() => api.GetAsync<Profile>("/api/profile");

    public Task<Profile> UpdateProfileAsync(string fullName, string? phone, string? gender, DateOnly? dateOfBirth, IFormFile? avatar, bool removeAvatar)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(fullName), "fullName" },
            { new StringContent(phone ?? string.Empty), "phone" },
            { new StringContent(gender ?? string.Empty), "gender" },
            { new StringContent(removeAvatar ? "true" : "false"), "removeAvatar" },
        };
        if (dateOfBirth is not null)
        {
            form.Add(new StringContent(dateOfBirth.Value.ToString("yyyy-MM-dd")), "dateOfBirth");
        }

        if (avatar is { Length: > 0 })
        {
            var file = new StreamContent(avatar.OpenReadStream());
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(avatar.ContentType);
            form.Add(file, "avatar", avatar.FileName);
        }

        return api.SendMultipartAsync<Profile>(HttpMethod.Put, "/api/profile", form);
    }

    public Task<List<Address>> GetAddressesAsync() => api.GetAsync<List<Address>>("/api/profile/addresses");

    public Task<Address> AddAddressAsync(object address) => api.PostAsync<Address>("/api/profile/addresses", address);

    public Task<Address> UpdateAddressAsync(int id, object address) => api.PutAsync<Address>($"/api/profile/addresses/{id}", address);

    public Task DeleteAddressAsync(int id) => api.DeleteAsync($"/api/profile/addresses/{id}");

    public Task SetDefaultAddressAsync(int id) => api.PutAsync($"/api/profile/addresses/{id}/default", null);

    public Task<List<WishlistItem>> GetWishlistAsync() => api.GetAsync<List<WishlistItem>>("/api/wishlist");

    public Task AddToWishlistAsync(int productId) => api.PostAsync($"/api/wishlist/{productId}", null);

    public Task RemoveFromWishlistAsync(int productId) => api.DeleteAsync($"/api/wishlist/{productId}");
}
