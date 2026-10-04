using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Daisy.Application.Features.Profile;

/// <summary>Profile and address book of the current customer (BR-08: owner from the token).</summary>
public sealed class ProfileService
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly TimeProvider _time;
    private readonly UploadOptions _uploads;
    private readonly IValidator<UpdateProfileRequest> _profileValidator;
    private readonly IValidator<AddressRequest> _addressValidator;

    public ProfileService(
        IApplicationDbContext db,
        IStorageService storage,
        TimeProvider time,
        IOptions<UploadOptions> uploads,
        IValidator<UpdateProfileRequest> profileValidator,
        IValidator<AddressRequest> addressValidator)
    {
        _db = db;
        _storage = storage;
        _time = time;
        _uploads = uploads.Value;
        _profileValidator = profileValidator;
        _addressValidator = addressValidator;
    }

    /// <summary>UC-08.</summary>
    public async Task<ProfileDto> GetProfileAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException();
        return ProfileDto.From(user);
    }

    /// <summary>UC-09. Email is not editable (BR-01).</summary>
    public async Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct)
    {
        await _profileValidator.ValidateAndThrowAsync(request, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException();

        string? oldAvatar = null;
        if (request.Avatar is not null)
        {
            var extension = await ImageFormat.EnsureImageAsync(request.Avatar, _uploads.MaxAvatarBytes, ct);
            await using var stream = request.Avatar.OpenReadStream();
            oldAvatar = user.AvatarUrl;
            user.AvatarUrl = await _storage.SaveAsync(stream, $"avatars/{userId}", extension, ct);
        }
        else if (request.RemoveAvatar)
        {
            oldAvatar = user.AvatarUrl;
            user.AvatarUrl = null;
        }

        user.FullName = request.FullName.Trim();
        user.Phone = ValidationRules.TrimToNull(request.Phone);
        user.Gender = request.Gender;
        user.DateOfBirth = request.DateOfBirth;
        user.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);

        if (oldAvatar is not null)
        {
            await _storage.DeleteAsync(oldAvatar, ct);
        }

        return ProfileDto.From(user);
    }

    /// <summary>UC-10. Default address first, then newest.</summary>
    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int userId, CancellationToken ct)
    {
        var addresses = await _db.UserAddresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AddressId)
            .ToListAsync(ct);
        return addresses.Select(AddressDto.From).ToList();
    }

    /// <summary>UC-11. The first address becomes the default (BR-09).</summary>
    public async Task<AddressDto> AddAddressAsync(int userId, AddressRequest request, CancellationToken ct)
    {
        await _addressValidator.ValidateAndThrowAsync(request, ct);
        var hasAny = await _db.UserAddresses.AnyAsync(a => a.UserId == userId, ct);
        var makeDefault = request.IsDefault || !hasAny;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        if (makeDefault)
        {
            await ClearDefaultAsync(userId, ct);
        }

        var address = new UserAddress
        {
            UserId = userId,
            CreatedAt = _time.GetVietnamNow(),
            IsDefault = makeDefault,
        };
        Apply(address, request);
        _db.UserAddresses.Add(address);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return AddressDto.From(address);
    }

    /// <summary>UC-11. Orders keep their own snapshot (BR-28).</summary>
    public async Task<AddressDto> UpdateAddressAsync(int userId, int addressId, AddressRequest request, CancellationToken ct)
    {
        await _addressValidator.ValidateAndThrowAsync(request, ct);
        var address = await FindOwnedAsync(userId, addressId, ct);
        Apply(address, request);
        await _db.SaveChangesAsync(ct);
        return AddressDto.From(address);
    }

    /// <summary>UC-11. Deleting the default leaves no default (BR-09).</summary>
    public async Task DeleteAddressAsync(int userId, int addressId, CancellationToken ct)
    {
        var address = await FindOwnedAsync(userId, addressId, ct);
        _db.UserAddresses.Remove(address);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-12. Two saves so the filtered unique index UX_UserAddresses_Default is never violated.</summary>
    public async Task SetDefaultAddressAsync(int userId, int addressId, CancellationToken ct)
    {
        var address = await FindOwnedAsync(userId, addressId, ct);
        if (address.IsDefault)
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await ClearDefaultAsync(userId, ct);
        address.IsDefault = true;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private Task<int> ClearDefaultAsync(int userId, CancellationToken ct) =>
        _db.UserAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    private async Task<UserAddress> FindOwnedAsync(int userId, int addressId, CancellationToken ct) =>
        await _db.UserAddresses.FirstOrDefaultAsync(a => a.AddressId == addressId && a.UserId == userId, ct)
        ?? throw new NotFoundException("Address not found.");

    private static void Apply(UserAddress address, AddressRequest request)
    {
        address.ReceiverName = request.ReceiverName.Trim();
        address.ReceiverPhone = request.ReceiverPhone.Replace(" ", string.Empty);
        address.Province = request.Province.Trim();
        address.District = request.District.Trim();
        address.Ward = request.Ward.Trim();
        address.StreetAddress = request.StreetAddress.Trim();
    }
}
