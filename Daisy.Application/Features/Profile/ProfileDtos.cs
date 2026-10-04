using Daisy.Application.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Profile;

public sealed record ProfileDto(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    string? AvatarUrl,
    UserRole Role,
    DateTime CreatedAt)
{
    public static ProfileDto From(User u) =>
        new(u.UserId, u.FullName, u.Email, u.Phone, u.Gender, u.DateOfBirth, u.AvatarUrl, u.Role, u.CreatedAt);
}

public sealed record UpdateProfileRequest(
    string FullName,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    FileUpload? Avatar,
    bool RemoveAvatar);

public sealed record AddressDto(
    int Id,
    string ReceiverName,
    string ReceiverPhone,
    string Province,
    string District,
    string Ward,
    string StreetAddress,
    string FullAddress,
    bool IsDefault)
{
    public static AddressDto From(UserAddress a) =>
        new(a.AddressId, a.ReceiverName, a.ReceiverPhone, a.Province, a.District, a.Ward, a.StreetAddress, a.FullAddress, a.IsDefault);
}

public sealed record AddressRequest(
    string ReceiverName,
    string ReceiverPhone,
    string Province,
    string District,
    string Ward,
    string StreetAddress,
    bool IsDefault = false);
