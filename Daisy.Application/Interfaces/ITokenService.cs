using Daisy.Domain.Entities;

namespace Daisy.Application.Interfaces;

public sealed record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>JWT access tokens and random refresh tokens (BR-04).</summary>
public interface ITokenService
{
    IssuedToken CreateAccessToken(User user);

    IssuedToken CreateRefreshToken();

    /// <summary>Random URL-safe token, e.g. for password reset (BR-05).</summary>
    string CreateRandomToken();

    /// <summary>SHA-256 hash stored instead of the raw token.</summary>
    string HashToken(string token);
}
