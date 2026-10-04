using Daisy.Domain.Enums;

namespace Daisy.Domain.Entities;

public class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; } = UserRole.Customer;

    public UserStatus Status { get; private set; } = UserStatus.Active;

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>SHA-256 hash of the password reset token (BR-05).</summary>
    public string? ResetPasswordToken { get; private set; }

    public DateTime? ResetPasswordExpiresAt { get; private set; }

    /// <summary>SHA-256 hash of the current refresh token (BR-04).</summary>
    public string? RefreshToken { get; private set; }

    public DateTime? RefreshTokenExpiresAt { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public bool IsLocked => Status == UserStatus.Locked;

    public void SetRefreshToken(string tokenHash, DateTime expiresAt)
    {
        RefreshToken = tokenHash;
        RefreshTokenExpiresAt = expiresAt;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
    }

    public bool HasValidRefreshToken(string tokenHash, DateTime now) =>
        RefreshToken is not null
        && RefreshTokenExpiresAt is not null
        && RefreshTokenExpiresAt > now
        && string.Equals(RefreshToken, tokenHash, StringComparison.Ordinal);

    public void SetResetPasswordToken(string tokenHash, DateTime expiresAt)
    {
        ResetPasswordToken = tokenHash;
        ResetPasswordExpiresAt = expiresAt;
    }

    public bool HasValidResetToken(string tokenHash, DateTime now) =>
        ResetPasswordToken is not null
        && ResetPasswordExpiresAt is not null
        && ResetPasswordExpiresAt > now
        && string.Equals(ResetPasswordToken, tokenHash, StringComparison.Ordinal);

    /// <summary>Sets a new password hash; the reset token and refresh token are revoked (BR-05).</summary>
    public void ChangePasswordHash(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        ResetPasswordToken = null;
        ResetPasswordExpiresAt = null;
        RevokeRefreshToken();
        UpdatedAt = now;
    }

    /// <summary>Locking revokes the refresh token immediately (BR-03).</summary>
    public void Lock(DateTime now)
    {
        Status = UserStatus.Locked;
        RevokeRefreshToken();
        UpdatedAt = now;
    }

    public void Unlock(DateTime now)
    {
        Status = UserStatus.Active;
        UpdatedAt = now;
    }
}
