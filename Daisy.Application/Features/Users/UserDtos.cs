using Daisy.Application.Common;
using Daisy.Application.Features.Orders;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Users;

/// <summary>OData entity of /odata/users; never contains password hash or tokens (BR-02).</summary>
public sealed class UserListDto
{
    public int Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public UserRole Role { get; init; }

    public UserStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record UserDetailDto(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    string? AvatarUrl,
    UserRole Role,
    UserStatus Status,
    DateTime CreatedAt,
    int OrderCount,
    decimal CompletedSpent,
    PagedResult<OrderSummaryDto> Orders);

public sealed record SetUserStatusRequest(UserStatus Status);

public sealed record SetUserRoleRequest(UserRole Role);
