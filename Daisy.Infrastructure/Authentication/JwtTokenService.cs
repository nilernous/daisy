using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Daisy.Infrastructure.Authentication;

/// <summary>Short-lived HS256 access tokens and random refresh tokens (BR-04).</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
    }

    public IssuedToken CreateAccessToken(User user)
    {
        var nowUtc = _time.GetUtcNow();
        var expiresUtc = nowUtc.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var credentials = new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            notBefore: nowUtc.UtcDateTime,
            expires: expiresUtc.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresUtc.ToVietnamTime());
    }

    public IssuedToken CreateRefreshToken() =>
        new(CreateRandomToken(), _time.GetUtcNow().AddDays(_options.RefreshTokenDays).ToVietnamTime());

    public string CreateRandomToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static SymmetricSecurityKey CreateSigningKey(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey) || Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Set it with dotnet user-secrets or an environment variable.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }
}
