using Daisy.Domain.Entities;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password, string ConfirmPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public sealed record UserSummaryDto(int Id, string FullName, string Email, UserRole Role, string? AvatarUrl)
{
    public static UserSummaryDto From(User user) =>
        new(user.UserId, user.FullName, user.Email, user.Role, user.AvatarUrl);
}

public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserSummaryDto User);

public sealed record MessageDto(string Message);
