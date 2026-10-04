namespace Daisy.Infrastructure.Authentication;

/// <summary>Configuration section "Jwt" (API_DESIGN §11). SigningKey comes from user secrets / environment.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Daisy.Api";

    public string Audience { get; set; } = "Daisy.Mvc";

    /// <summary>At least 32 characters (256 bits) for HS256.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
