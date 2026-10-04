using System.Text.Encodings.Web;
using System.Text.Unicode;
using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
services.AddSingleton<MediaUrls>();
services.AddMemoryCache();
services.AddHttpContextAccessor();

// Vietnamese text is written as is instead of as &#x..; entities; HTML-sensitive characters are still encoded.
services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

services
    .AddControllersWithViews(o =>
    {
        o.Filters.Add<ApiExceptionFilter>();
        o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    });

// Encrypted auth cookie holding the API tokens (API_DESIGN §3.1). Customers, Staff and Admin share it.
services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = ".Daisy.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.LoginPath = "/account/login";
        o.LogoutPath = "/account/logout";
        o.AccessDeniedPath = "/account/access-denied";
        o.SlidingExpiration = false;
    });
services.AddAuthorization();

// HttpClient to Daisy.Api. MVC never touches the database (ARCHITECTURE §24).
void ConfigureApi(IServiceProvider sp, HttpClient client)
{
    client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ApiOptions>>().Value.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
}

HttpMessageHandler PrimaryHandler(IServiceProvider sp)
{
    var handler = new HttpClientHandler { AllowAutoRedirect = false };
    if (sp.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
    {
        // The API uses the ASP.NET Core development certificate locally.
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
}

services.AddTransient<ApiAuthHandler>();
services.AddHttpClient(ApiAuthHandler.RawClientName, ConfigureApi).ConfigurePrimaryHttpMessageHandler(PrimaryHandler);
services.AddHttpClient<ApiClient>(ConfigureApi)
    .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
    .AddHttpMessageHandler<ApiAuthHandler>();

services.AddScoped<AuthApiService>();
services.AddScoped<ProductApiService>();
services.AddScoped<CartApiService>();
services.AddScoped<OrderApiService>();
services.AddScoped<PaymentApiService>();
services.AddScoped<ProfileApiService>();
services.AddScoped<AdminApiService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/home/status/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<CartSessionMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapAreaControllerRoute("admin", "Admin", "admin/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
