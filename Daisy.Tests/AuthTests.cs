using System.Net;
using Daisy.Tests.Infrastructure;

namespace Daisy.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests
{
    private readonly DaisyApiFactory _factory;

    public AuthTests(DaisyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_logout_revokes_it()
    {
        var (_, email, _) = await _factory.NewCustomerAsync();
        var client = new ApiClient(_factory.CreateClient());
        var login = await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/login", new { email, password = "Secret123" }));
        Assert.Equal("Customer", login!["user"]!["role"]!.GetValue<string>());
        Assert.EndsWith("+07:00", login["accessTokenExpiresAt"]!.GetValue<string>());

        var first = login["refreshToken"]!.GetValue<string>();
        var refreshed = await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/refresh", new { refreshToken = first }));
        var second = refreshed!["refreshToken"]!.GetValue<string>();
        Assert.NotEqual(first, second);

        // BR-04: the old refresh token stops working immediately.
        var reuse = await client.ExpectAsync(HttpStatusCode.Unauthorized, client.PostAsync("/api/auth/refresh", new { refreshToken = first }));
        Assert.Equal("INVALID_REFRESH_TOKEN", reuse!["code"]!.GetValue<string>());

        var signedIn = new ApiClient(_factory.CreateClient(), refreshed["accessToken"]!.GetValue<string>());
        await signedIn.ExpectAsync(HttpStatusCode.NoContent, signedIn.PostAsync("/api/auth/logout"));
        await client.ExpectAsync(HttpStatusCode.Unauthorized, client.PostAsync("/api/auth/refresh", new { refreshToken = second }));
    }

    [Fact]
    public async Task Register_rejects_an_email_that_differs_only_in_case()
    {
        var (_, email, _) = await _factory.NewCustomerAsync();
        var client = new ApiClient(_factory.CreateClient());
        var body = await client.ExpectAsync(HttpStatusCode.Conflict, client.PostAsync("/api/auth/register", new
        {
            fullName = "Trùng Email",
            email = email.ToUpperInvariant(),
            password = "Secret123",
            confirmPassword = "Secret123",
        }));
        Assert.Equal("EMAIL_ALREADY_EXISTS", body!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validation_errors_use_the_standard_body_with_camelCase_fields()
    {
        var client = new ApiClient(_factory.CreateClient());
        var body = await client.ExpectAsync(HttpStatusCode.BadRequest, client.PostAsync("/api/auth/register", new
        {
            fullName = "",
            email = "not-an-email",
            password = "short",
            confirmPassword = "other",
        }));
        Assert.Equal("VALIDATION_FAILED", body!["code"]!.GetValue<string>());
        var errors = body["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("fullName"));
        Assert.True(errors.ContainsKey("email"));
        Assert.True(errors.ContainsKey("password"));
        Assert.True(errors.ContainsKey("confirmPassword"));
    }

    [Fact]
    public async Task Locked_account_cannot_sign_in_and_unlocking_restores_access()
    {
        var (customer, email, _) = await _factory.NewCustomerAsync();
        var profile = await customer.ExpectAsync(HttpStatusCode.OK, customer.GetAsync("/api/profile"));
        var userId = profile!["id"]!.GetValue<int>();
        var admin = await _factory.AsAdminAsync();

        await admin.ExpectAsync(HttpStatusCode.NoContent, admin.PatchAsync($"/api/admin/users/{userId}/status", new { status = "Locked" }));
        var anonymous = new ApiClient(_factory.CreateClient());
        var locked = await anonymous.ExpectAsync(HttpStatusCode.Forbidden, anonymous.PostAsync("/api/auth/login", new { email, password = "Secret123" }));
        Assert.Equal("ACCOUNT_LOCKED", locked!["code"]!.GetValue<string>());

        await admin.ExpectAsync(HttpStatusCode.NoContent, admin.PatchAsync($"/api/admin/users/{userId}/status", new { status = "Active" }));
        await anonymous.ExpectAsync(HttpStatusCode.OK, anonymous.PostAsync("/api/auth/login", new { email, password = "Secret123" }));
    }

    [Fact]
    public async Task Admin_cannot_lock_or_demote_themselves()
    {
        var admin = await _factory.AsAdminAsync();
        var body = await admin.ExpectAsync(HttpStatusCode.BadRequest, admin.PatchAsync("/api/admin/users/1/role", new { role = "Customer" }));
        Assert.Equal("CANNOT_MODIFY_SELF", body!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Password_reset_link_works_once_and_revokes_sessions()
    {
        var (_, email, _) = await _factory.NewCustomerAsync();
        var client = new ApiClient(_factory.CreateClient());

        // BR-05: same answer for unknown emails.
        var unknown = await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/forgot-password", new { email = "nobody@test.local" }));
        var known = await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/forgot-password", new { email }));
        Assert.Equal(unknown!["message"]!.GetValue<string>(), known!["message"]!.GetValue<string>());

        var token = _factory.Emails.LastResetToken(email);
        Assert.False(string.IsNullOrEmpty(token));
        await client.ExpectAsync(HttpStatusCode.NoContent, client.PostAsync("/api/auth/reset-password", new
        {
            token,
            newPassword = "NewSecret456",
            confirmPassword = "NewSecret456",
        }));

        var reused = await client.ExpectAsync(HttpStatusCode.BadRequest, client.PostAsync("/api/auth/reset-password", new
        {
            token,
            newPassword = "Another789",
            confirmPassword = "Another789",
        }));
        Assert.Equal("INVALID_RESET_TOKEN", reused!["code"]!.GetValue<string>());

        await client.ExpectAsync(HttpStatusCode.Unauthorized, client.PostAsync("/api/auth/login", new { email, password = "Secret123" }));
        await client.ExpectAsync(HttpStatusCode.OK, client.PostAsync("/api/auth/login", new { email, password = "NewSecret456" }));
    }

    [Fact]
    public async Task Roles_are_enforced_per_policy()
    {
        var (customer, _, _) = await _factory.NewCustomerAsync();
        var staff = await _factory.AsStaffAsync();

        await customer.ExpectAsync(HttpStatusCode.Forbidden, customer.GetAsync("/api/admin/inventory/low-stock"));
        await staff.ExpectAsync(HttpStatusCode.OK, staff.GetAsync("/api/admin/inventory/low-stock"));
        await staff.ExpectAsync(HttpStatusCode.Forbidden, staff.GetAsync("/api/admin/vouchers"));
        await staff.ExpectAsync(HttpStatusCode.Forbidden, staff.PutAsync("/api/admin/products/1/sale", new { salePrice = 1 }));

        var anonymous = new ApiClient(_factory.CreateClient());
        var body = await anonymous.ExpectAsync(HttpStatusCode.Unauthorized, anonymous.GetAsync("/api/profile"));
        Assert.Equal("UNAUTHORIZED", body!["code"]!.GetValue<string>());
    }
}
