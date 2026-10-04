using Daisy.Application.Features.Auth;
using Daisy.Application.Features.Cart;
using Daisy.Application.Features.Categories;
using Daisy.Application.Features.Checkout;
using Daisy.Application.Features.Dashboard;
using Daisy.Application.Features.Inventory;
using Daisy.Application.Features.Orders;
using Daisy.Application.Features.Payments;
using Daisy.Application.Features.Products;
using Daisy.Application.Features.Profile;
using Daisy.Application.Features.Reviews;
using Daisy.Application.Features.Users;
using Daisy.Application.Features.Vouchers;
using Daisy.Application.Features.Wishlist;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Daisy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // One message per field: stop at the first failing rule of a property.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<CatalogVisibility>();
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductVariantService>();
        services.AddScoped<ProductImageService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<WishlistService>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderExpiryService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<VoucherService>();
        services.AddScoped<UserService>();
        services.AddScoped<DashboardService>();

        return services;
    }
}
