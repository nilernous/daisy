namespace Daisy.Api.Infrastructure;

/// <summary>Authorization policies of API_DESIGN §3.3 (BR-07).</summary>
public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string Customer = "Customer";
    public const string Staff = "Staff";
    public const string Admin = "Admin";
}

/// <summary>Rate limiter policies of API_DESIGN §10.</summary>
public static class RateLimits
{
    public const string Auth = "auth";
    public const string Password = "password";
    public const string Voucher = "voucher";
}
