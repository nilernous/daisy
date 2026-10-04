using System.Net.Http.Headers;
using Daisy.Mvc.Models.Api;

namespace Daisy.Mvc.Services;

/// <summary>Staff and Admin endpoints (/api/admin, /odata) for the Admin area (UC-43 – UC-71).</summary>
public sealed class AdminApiService(ApiClient api)
{
    // ---- Categories (UC-43, UC-44) ------------------------------------------------------
    public Task<List<CategoryAdminNode>> GetCategoriesAsync() => api.GetAsync<List<CategoryAdminNode>>("/api/admin/categories");

    public Task<CategoryInfo> GetCategoryAsync(int id) => api.GetAsync<CategoryInfo>($"/api/admin/categories/{id}");

    public Task<CategoryInfo> CreateCategoryAsync(object body) => api.PostAsync<CategoryInfo>("/api/admin/categories", body);

    public Task<CategoryInfo> UpdateCategoryAsync(int id, object body) => api.PutAsync<CategoryInfo>($"/api/admin/categories/{id}", body);

    public Task SetCategoryVisibilityAsync(int id, bool isActive) => api.PatchAsync($"/api/admin/categories/{id}/visibility", new { isActive });

    // ---- Products, variants, images (UC-45 – UC-50) -------------------------------------------
    public Task<PagedResult<ProductAdminListItem>> GetProductsAsync(string query) =>
        api.GetAsync<PagedResult<ProductAdminListItem>>($"/api/admin/products{query}");

    public Task<ProductAdmin> GetProductAsync(int id) => api.GetAsync<ProductAdmin>($"/api/admin/products/{id}");

    public Task<ProductAdmin> CreateProductAsync(object body) => api.PostAsync<ProductAdmin>("/api/admin/products", body);

    public Task<ProductAdmin> UpdateProductAsync(int id, object body) => api.PutAsync<ProductAdmin>($"/api/admin/products/{id}", body);

    public Task SetProductVisibilityAsync(int id, bool isActive) => api.PatchAsync($"/api/admin/products/{id}/visibility", new { isActive });

    public Task SetProductFeaturedAsync(int id, bool isFeatured) => api.PatchAsync($"/api/admin/products/{id}/featured", new { isFeatured });

    public Task SetSaleAsync(int id, decimal salePrice, DateTimeOffset? startAt, DateTimeOffset? endAt) =>
        api.PutAsync($"/api/admin/products/{id}/sale", new { salePrice, startAt, endAt });

    public Task ClearSaleAsync(int id) => api.DeleteAsync($"/api/admin/products/{id}/sale");

    public Task<VariantAdmin> CreateVariantAsync(int productId, object body) => api.PostAsync<VariantAdmin>($"/api/admin/products/{productId}/variants", body);

    public Task<VariantAdmin> UpdateVariantAsync(int variantId, object body) => api.PutAsync<VariantAdmin>($"/api/admin/variants/{variantId}", body);

    public Task SetVariantVisibilityAsync(int variantId, bool isActive) => api.PatchAsync($"/api/admin/variants/{variantId}/visibility", new { isActive });

    public Task<List<ProductImage>> UploadImagesAsync(int productId, IEnumerable<IFormFile> files)
    {
        var form = new MultipartFormDataContent();
        foreach (var file in files.Where(f => f.Length > 0))
        {
            var content = new StreamContent(file.OpenReadStream());
            content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(content, "files", file.FileName);
        }

        return api.SendMultipartAsync<List<ProductImage>>(HttpMethod.Post, $"/api/admin/products/{productId}/images", form);
    }

    public Task SetThumbnailAsync(int imageId) => api.PatchAsync($"/api/admin/images/{imageId}/thumbnail", null);

    public Task ReorderImagesAsync(int productId, IEnumerable<int> imageIds) => api.PutAsync($"/api/admin/products/{productId}/images/order", new { imageIds });

    public Task SetImageVariantAsync(int imageId, int? variantId) => api.PatchAsync($"/api/admin/images/{imageId}/variant", new { variantId });

    public Task DeleteImageAsync(int imageId) => api.DeleteAsync($"/api/admin/images/{imageId}");

    // ---- Inventory (UC-51, UC-52) -------------------------------------------------------------
    public Task UpdateStockAsync(int variantId, int stockQuantity, int expectedCurrentStock) =>
        api.PutAsync($"/api/admin/variants/{variantId}/stock", new { stockQuantity, expectedCurrentStock });

    public Task<List<LowStockItem>> GetLowStockAsync(bool includeHidden) =>
        api.GetAsync<List<LowStockItem>>($"/api/admin/inventory/low-stock?includeHidden={includeHidden.ToString().ToLowerInvariant()}");

    // ---- Orders and payments (UC-53 – UC-57) ------------------------------------------------------
    public Task<ODataPage<OrderAdminListItem>> QueryOrdersAsync(ODataQuery query) => api.ODataAsync<OrderAdminListItem>("orders", query);

    public Task<OrderAdminDetail> GetOrderAsync(int id) => api.GetAsync<OrderAdminDetail>($"/api/admin/orders/{id}");

    public Task UpdateOrderStatusAsync(int id, OrderStatus status) => api.PatchAsync($"/api/admin/orders/{id}/status", new { status });

    public Task<CanceledOrder> CancelOrderAsync(int id, string reason) => api.PostAsync<CanceledOrder>($"/api/admin/orders/{id}/cancel", new { reason });

    public Task UpdatePaymentAsync(int id, string action, string? transactionCode) =>
        api.PatchAsync($"/api/admin/orders/{id}/payment", new { action, transactionCode });

    // ---- Payment transactions (UC-71) ----------------------------------------------------------
    public Task<PagedResult<PaymentTransaction>> GetTransactionsAsync(string query) =>
        api.GetAsync<PagedResult<PaymentTransaction>>($"/api/admin/payment-transactions{query}");

    public Task<PaymentTransaction> ApplyTransactionAsync(int id, int orderId, string? note) =>
        api.PostAsync<PaymentTransaction>($"/api/admin/payment-transactions/{id}/apply", new { orderId, note });

    public Task<PaymentTransaction> ResolveTransactionAsync(int id, PaymentResolution resolution, string note) =>
        api.PatchAsync<PaymentTransaction>($"/api/admin/payment-transactions/{id}/resolution", new { resolution, note });

    // ---- Vouchers (UC-58 – UC-60) --------------------------------------------------------------
    public Task<PagedResult<Voucher>> GetVouchersAsync(string query) => api.GetAsync<PagedResult<Voucher>>($"/api/admin/vouchers{query}");

    public Task<Voucher> GetVoucherAsync(int id) => api.GetAsync<Voucher>($"/api/admin/vouchers/{id}");

    public Task<Voucher> CreateVoucherAsync(object body) => api.PostAsync<Voucher>("/api/admin/vouchers", body);

    public Task<Voucher> UpdateVoucherAsync(int id, object body) => api.PutAsync<Voucher>($"/api/admin/vouchers/{id}", body);

    public Task SetVoucherActiveAsync(int id, bool isActive) => api.PatchAsync($"/api/admin/vouchers/{id}/active", new { isActive });

    public Task<VoucherUsage> GetVoucherUsageAsync(int id, int page) => api.GetAsync<VoucherUsage>($"/api/admin/vouchers/{id}/usage?page={page}&pageSize=20");

    // ---- Reviews (UC-61, UC-62) ------------------------------------------------------------------
    public Task<ODataPage<ReviewAdminItem>> QueryReviewsAsync(ODataQuery query) => api.ODataAsync<ReviewAdminItem>("reviews", query);

    public Task SetReviewVisibilityAsync(int id, bool isVisible) => api.PatchAsync($"/api/admin/reviews/{id}/visibility", new { isVisible });

    // ---- Users (UC-63 – UC-66) -------------------------------------------------------------------
    public Task<ODataPage<UserListItem>> QueryUsersAsync(ODataQuery query) => api.ODataAsync<UserListItem>("users", query);

    public Task<UserDetail> GetUserAsync(int id, int ordersPage) => api.GetAsync<UserDetail>($"/api/admin/users/{id}?ordersPage={ordersPage}&ordersPageSize=10");

    public Task SetUserStatusAsync(int id, UserStatus status) => api.PatchAsync($"/api/admin/users/{id}/status", new { status });

    public Task SetUserRoleAsync(int id, UserRole role) => api.PatchAsync($"/api/admin/users/{id}/role", new { role });

    // ---- Dashboard (UC-67 – UC-70) -----------------------------------------------------------------
    public Task<List<RevenuePoint>> GetRevenueAsync(string range) => api.GetAsync<List<RevenuePoint>>($"/api/admin/dashboard/revenue{range}");

    public Task<List<OrderStatusCount>> GetOrdersByStatusAsync(string range) => api.GetAsync<List<OrderStatusCount>>($"/api/admin/dashboard/orders-by-status{range}");

    public Task<List<TopProduct>> GetTopProductsAsync(string range) => api.GetAsync<List<TopProduct>>($"/api/admin/dashboard/top-products{range}");

    public Task<List<NewCustomerPoint>> GetNewCustomersAsync(string range) => api.GetAsync<List<NewCustomerPoint>>($"/api/admin/dashboard/new-customers{range}");
}
