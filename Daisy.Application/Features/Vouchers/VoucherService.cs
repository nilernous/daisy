using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Daisy.Application.Features.Vouchers;

/// <summary>Admin voucher management (UC-58 – UC-60). Vouchers are deactivated, never deleted (BR-11).</summary>
public sealed class VoucherService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;
    private readonly IValidator<VoucherRequest> _validator;

    public VoucherService(IApplicationDbContext db, TimeProvider time, IValidator<VoucherRequest> validator)
    {
        _db = db;
        _time = time;
        _validator = validator;
    }

    public async Task<PagedResult<VoucherDto>> GetListAsync(VoucherQuery query, CancellationToken ct)
    {
        query.ValidatePage();
        var now = _time.GetVietnamNow();
        var vouchers = _db.Vouchers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            vouchers = vouchers.Where(v => v.Code.Contains(term) || v.Name.Contains(term));
        }

        if (query.IsActive is bool isActive)
        {
            vouchers = vouchers.Where(v => v.IsActive == isActive);
        }

        vouchers = query.State switch
        {
            null or "" => vouchers,
            VoucherState.Upcoming => vouchers.Where(v => v.StartAt > now),
            VoucherState.Running => vouchers.Where(v => v.StartAt <= now && v.EndAt >= now),
            VoucherState.Expired => vouchers.Where(v => v.EndAt < now),
            _ => throw new ValidationException([new ValidationFailure("state", "State must be Upcoming, Running or Expired.")]),
        };

        var page = await vouchers
            .OrderByDescending(v => v.StartAt).ThenByDescending(v => v.VoucherId)
            .ToPagedResultAsync(query, ct);
        return page.Map(v => VoucherDto.From(v, now));
    }

    public async Task<VoucherDto> GetAsync(int id, CancellationToken ct)
    {
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.VoucherId == id, ct)
            ?? throw new NotFoundException("Voucher not found.");
        return VoucherDto.From(voucher, _time.GetVietnamNow());
    }

    /// <summary>UC-58.</summary>
    public async Task<VoucherDto> CreateAsync(VoucherRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeFreeAsync(code, null, ct);

        var voucher = new Voucher { Code = code, DiscountType = request.DiscountType };
        Apply(voucher, request);
        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync(ct);
        return VoucherDto.From(voucher, _time.GetVietnamNow());
    }

    /// <summary>
    /// UC-58. UsageLimit cannot go below UsedCount; code and type cannot change once used.
    /// </summary>
    public async Task<VoucherDto> UpdateAsync(int id, VoucherRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id, ct)
            ?? throw new NotFoundException("Voucher not found.");
        var code = request.Code.Trim().ToUpperInvariant();
        var errors = new List<ValidationFailure>();

        if (request.UsageLimit is not null && request.UsageLimit < voucher.UsedCount)
        {
            errors.Add(new("usageLimit", $"Usage limit cannot be below the {voucher.UsedCount} uses already made."));
        }

        if (voucher.UsedCount > 0 && code != voucher.Code)
        {
            errors.Add(new("code", "The code cannot change after the voucher has been used."));
        }

        if (voucher.UsedCount > 0 && request.DiscountType != voucher.DiscountType)
        {
            errors.Add(new("discountType", "The discount type cannot change after the voucher has been used."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        await EnsureCodeFreeAsync(code, id, ct);
        voucher.Code = code;
        voucher.DiscountType = request.DiscountType;
        Apply(voucher, request);
        await _db.SaveChangesAsync(ct);
        return VoucherDto.From(voucher, _time.GetVietnamNow());
    }

    /// <summary>UC-59. Inactive vouchers cannot be applied to new orders (BR-26).</summary>
    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct)
    {
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id, ct)
            ?? throw new NotFoundException("Voucher not found.");
        voucher.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-60. Canceled orders are listed but not counted (BR-36).</summary>
    public async Task<VoucherUsageDto> GetUsageAsync(int id, PageRequest page, CancellationToken ct)
    {
        page.ValidatePage();
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.VoucherId == id, ct)
            ?? throw new NotFoundException("Voucher not found.");

        var orders = _db.Orders.AsNoTracking().Where(o => o.VoucherId == id);
        var totalDiscount = await orders
            .Where(o => o.OrderStatus != OrderStatus.Canceled)
            .SumAsync(o => (decimal?)o.DiscountAmount, ct) ?? 0;

        var list = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new VoucherOrderDto(
                o.OrderId, o.OrderCode, o.User!.FullName, o.DiscountAmount, o.TotalAmount, o.OrderStatus, o.CreatedAt))
            .ToPagedResultAsync(page, ct);

        return new VoucherUsageDto(VoucherDto.From(voucher, _time.GetVietnamNow()), totalDiscount, list);
    }

    private async Task EnsureCodeFreeAsync(string code, int? exceptId, CancellationToken ct)
    {
        if (await _db.Vouchers.AnyAsync(v => v.Code == code && v.VoucherId != exceptId, ct))
        {
            throw new ConflictException(ErrorCodes.VoucherCodeExists, "Another voucher already uses this code.");
        }
    }

    private static void Apply(Voucher voucher, VoucherRequest request)
    {
        voucher.Name = request.Name.Trim();
        voucher.DiscountValue = request.DiscountValue;
        voucher.MaxDiscountAmount = request.DiscountType == DiscountType.Percent ? request.MaxDiscountAmount : null;
        voucher.MinOrderAmount = request.MinOrderAmount;
        voucher.UsageLimit = request.UsageLimit;
        voucher.StartAt = request.StartAt;
        voucher.EndAt = request.EndAt;
        voucher.IsActive = request.IsActive;
    }
}
