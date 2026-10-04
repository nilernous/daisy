using Daisy.Application.Common;
using Daisy.Application.Features.Orders;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Users;

/// <summary>Admin user management (UC-63 – UC-66). Users are locked, never deleted (BR-11).</summary>
public sealed class UserService
{
    private readonly IApplicationDbContext _db;
    private readonly OrderService _orders;
    private readonly TimeProvider _time;

    public UserService(IApplicationDbContext db, OrderService orders, TimeProvider time)
    {
        _db = db;
        _orders = orders;
        _time = time;
    }

    /// <summary>Query behind GET /odata/users (UC-63).</summary>
    public IQueryable<UserListDto> QueryAdminList() =>
        _db.Users.AsNoTracking().Select(u => new UserListDto
        {
            Id = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Role = u.Role,
            Status = u.Status,
            CreatedAt = u.CreatedAt,
        });

    /// <summary>UC-64.</summary>
    public async Task<UserDetailDto> GetDetailAsync(int id, PageRequest ordersPage, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id, ct)
            ?? throw new NotFoundException("User not found.");

        var orderCount = await _db.Orders.CountAsync(o => o.UserId == id, ct);
        var spent = await _db.Orders
            .Where(o => o.UserId == id && o.OrderStatus == OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;
        var orders = await _orders.GetMyOrdersAsync(id, new OrderListQuery { Page = ordersPage.Page, PageSize = ordersPage.PageSize }, ct);

        return new UserDetailDto(
            user.UserId, user.FullName, user.Email, user.Phone, user.Gender, user.DateOfBirth, user.AvatarUrl,
            user.Role, user.Status, user.CreatedAt, orderCount, spent, orders);
    }

    /// <summary>UC-65. Locking revokes the refresh token (BR-03); an Admin cannot lock themselves.</summary>
    public async Task SetStatusAsync(int currentUserId, int id, SetUserStatusRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status))
        {
            throw new ValidationException([new ValidationFailure("status", "Status must be Active or Locked.")]);
        }

        if (id == currentUserId)
        {
            throw new BadRequestException(ErrorCodes.CannotModifySelf, "You cannot lock or unlock your own account.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id, ct)
            ?? throw new NotFoundException("User not found.");
        var now = _time.GetVietnamNow();
        if (request.Status == UserStatus.Locked)
        {
            user.Lock(now);
        }
        else
        {
            user.Unlock(now);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-66. Applies at the next token refresh (BR-04); an Admin cannot change their own role.</summary>
    public async Task SetRoleAsync(int currentUserId, int id, SetUserRoleRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Role))
        {
            throw new ValidationException([new ValidationFailure("role", "Role must be Customer, Staff or Admin.")]);
        }

        if (id == currentUserId)
        {
            throw new BadRequestException(ErrorCodes.CannotModifySelf, "You cannot change your own role.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id, ct)
            ?? throw new NotFoundException("User not found.");
        user.Role = request.Role;
        user.UpdatedAt = _time.GetVietnamNow();
        await _db.SaveChangesAsync(ct);
    }
}
