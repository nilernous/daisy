using System.Security.Cryptography;
using Daisy.Application.Common;
using Daisy.Application.Features.Cart;
using Daisy.Application.Features.Profile;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Daisy.Application.Features.Checkout;

/// <summary>Checkout (UC-32 – UC-35). Every amount is calculated by the backend (BR-24).</summary>
public sealed class CheckoutService
{
    private static readonly PaymentMethod[] PaymentMethods = [PaymentMethod.COD, PaymentMethod.Online];
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IApplicationDbContext _db;
    private readonly CartService _cart;
    private readonly ProfileService _profile;
    private readonly TimeProvider _time;
    private readonly CheckoutOptions _checkout;
    private readonly PaymentDeadlineOptions _deadline;
    private readonly IValidator<PlaceOrderRequest> _placeValidator;
    private readonly IValidator<ApplyVoucherRequest> _voucherValidator;

    public CheckoutService(
        IApplicationDbContext db,
        CartService cart,
        ProfileService profile,
        TimeProvider time,
        IOptions<CheckoutOptions> checkout,
        IOptions<PaymentDeadlineOptions> deadline,
        IValidator<PlaceOrderRequest> placeValidator,
        IValidator<ApplyVoucherRequest> voucherValidator)
    {
        _db = db;
        _cart = cart;
        _profile = profile;
        _time = time;
        _checkout = checkout.Value;
        _deadline = deadline.Value;
        _placeValidator = placeValidator;
        _voucherValidator = voucherValidator;
    }

    /// <summary>UC-32. Preview of the selected cart lines, without a voucher.</summary>
    public async Task<CheckoutDto> GetCheckoutAsync(int userId, CancellationToken ct)
    {
        var items = await GetSelectedItemsAsync(userId, ct);
        var addresses = await _profile.GetAddressesAsync(userId, ct);
        var subtotal = items.Where(i => i.IsAvailable).Sum(i => i.LineTotal);
        var totals = BuildTotals(subtotal, null);

        return new CheckoutDto(
            items,
            addresses,
            addresses.FirstOrDefault(a => a.IsDefault)?.Id,
            PaymentMethods,
            totals,
            CanPlaceOrder: items.Count > 0 && items.All(i => i.IsAvailable) && addresses.Count > 0);
    }

    /// <summary>UC-33. Previews the totals with a voucher; nothing is reserved (BR-26, BR-27).</summary>
    public async Task<CheckoutTotalsDto> PreviewVoucherAsync(int userId, ApplyVoucherRequest request, CancellationToken ct)
    {
        await _voucherValidator.ValidateAndThrowAsync(request, ct);
        var items = await GetSelectedItemsAsync(userId, ct);
        if (items.Count == 0)
        {
            throw new ConflictException(ErrorCodes.NoItemsSelected, "Select at least one item in your cart.");
        }

        var subtotal = items.Where(i => i.IsAvailable).Sum(i => i.LineTotal);
        var voucher = await FindVoucherAsync(request.Code, ct);
        var reason = voucher.GetIneligibilityReason(subtotal, _time.GetVietnamNow());
        if (reason is not null)
        {
            throw new BadRequestException(ErrorCodes.VoucherNotEligible, VoucherMessage(reason, voucher), new { reason });
        }

        return BuildTotals(subtotal, voucher);
    }

    /// <summary>
    /// UC-34 / UC-35. Validates items, voucher and total, then creates the order, deducts stock,
    /// increases SoldCount and voucher usage and clears the ordered cart lines in one transaction (BR-30).
    /// </summary>
    public async Task<PlaceOrderResultDto> PlaceOrderAsync(int userId, PlaceOrderRequest request, CancellationToken ct)
    {
        await _placeValidator.ValidateAndThrowAsync(request, ct);
        var now = _time.GetVietnamNow();

        var lines = (await _cart.LoadLinesAsync(CartOwner.ForUser(userId), ct)).Where(l => l.IsSelected).ToList();
        if (lines.Count == 0)
        {
            throw new ConflictException(ErrorCodes.NoItemsSelected, "Select at least one item in your cart.");
        }

        var address = await _db.UserAddresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AddressId == request.AddressId && a.UserId == userId, ct)
            ?? throw new NotFoundException("Address not found.");

        var cart = await _cart.BuildCartAsync(lines, ct);
        var unavailable = cart.Items
            .Where(i => !i.IsAvailable)
            .Select(i => new UnavailableItemDto(i.Id, i.UnavailableReason!, Math.Max(0, i.StockQuantity)))
            .ToList();
        if (unavailable.Count > 0)
        {
            var code = unavailable.All(u => u.Reason == CartUnavailableReason.Hidden)
                ? ErrorCodes.ItemUnavailable
                : ErrorCodes.InsufficientStock;
            throw new ConflictException(code, "Some items are unavailable or do not have enough stock.", new { items = unavailable });
        }

        var subtotal = cart.Items.Sum(i => i.LineTotal);
        Voucher? voucher = null;
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            voucher = await FindVoucherAsync(request.VoucherCode, ct);
            var reason = voucher.GetIneligibilityReason(subtotal, now);
            if (reason is not null)
            {
                throw new ConflictException(ErrorCodes.VoucherNotEligible, VoucherMessage(reason, voucher), new { reason });
            }
        }

        var totals = BuildTotals(subtotal, voucher);
        if (totals.TotalAmount != request.ExpectedTotal)
        {
            throw new ConflictException(
                ErrorCodes.TotalChanged,
                "The order total has changed. Review the new total and confirm again.",
                new { newTotal = totals.TotalAmount });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var order = Order.Place(
            userId,
            await GenerateOrderCodeAsync(now, ct),
            address,
            request.PaymentMethod,
            voucher?.VoucherId,
            totals.SubtotalAmount,
            totals.DiscountAmount,
            totals.ShippingFee,
            ValidationRules.TrimToNull(request.CustomerNote),
            now,
            _deadline.PaymentTimeout);

        foreach (var item in cart.Items)
        {
            var line = lines.First(l => l.CartItemId == item.Id);
            order.Items.Add(new OrderItem
            {
                VariantId = item.VariantId,
                ProductNameSnapshot = item.ProductName,
                SizeSnapshot = item.Size,
                ColorSnapshot = item.Color,
                ImageUrlSnapshot = item.ThumbnailUrl,
                UnitPrice = item.UnitPrice,
                Quantity = line.Quantity,
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        foreach (var item in cart.Items)
        {
            var quantity = item.Quantity;
            var deducted = await _db.ProductVariants
                .Where(v => v.VariantId == item.VariantId && v.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - quantity), ct);
            if (deducted == 0)
            {
                await transaction.RollbackAsync(ct);
                throw new ConflictException(ErrorCodes.ConcurrencyConflict, "Stock changed while you were checking out. Please review your cart.");
            }

            await _db.Products
                .Where(p => p.ProductId == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + quantity), ct);
        }

        if (voucher is not null)
        {
            var used = await _db.Vouchers
                .Where(v => v.VoucherId == voucher.VoucherId && v.IsActive
                    && (v.UsageLimit == null || v.UsedCount < v.UsageLimit))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), ct);
            if (used == 0)
            {
                await transaction.RollbackAsync(ct);
                throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The voucher was used up while you were checking out. Please review your order.");
            }
        }

        var orderedLineIds = lines.Select(l => l.CartItemId).ToList();
        await _db.CartItems.Where(c => orderedLineIds.Contains(c.CartItemId)).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        return new PlaceOrderResultDto(
            order.OrderId,
            order.OrderCode,
            order.OrderStatus,
            order.PaymentMethod,
            order.PaymentStatus,
            order.TotalAmount,
            order.PaymentExpiresAt,
            order.PaymentMethod == PaymentMethod.Online ? $"/api/orders/{order.OrderId}/payment-qr" : null);
    }

    private async Task<IReadOnlyList<CheckoutItemDto>> GetSelectedItemsAsync(int userId, CancellationToken ct)
    {
        var lines = (await _cart.LoadLinesAsync(CartOwner.ForUser(userId), ct)).Where(l => l.IsSelected).ToList();
        var cart = await _cart.BuildCartAsync(lines, ct);
        return cart.Items
            .Select(i => new CheckoutItemDto(
                i.Id, i.VariantId, i.ProductName, i.Size, i.Color, i.ThumbnailUrl, i.UnitPrice, i.Quantity,
                i.LineTotal, i.IsAvailable, i.UnavailableReason))
            .ToList();
    }

    private CheckoutTotalsDto BuildTotals(decimal subtotal, Voucher? voucher)
    {
        var discount = voucher?.CalculateDiscount(subtotal) ?? 0;
        var shipping = subtotal > 0 ? _checkout.CalculateShippingFee(subtotal) : 0;
        var total = Math.Max(0, subtotal - discount + shipping);
        return new CheckoutTotalsDto(subtotal, discount, shipping, total, voucher is null ? null : VoucherInfoDto.From(voucher));
    }

    private async Task<Voucher> FindVoucherAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == normalized, ct)
            ?? throw new NotFoundException("The voucher code is invalid.", ErrorCodes.VoucherNotFound);
    }

    /// <summary>DS + yyMMdd + 4 random characters, unique (BR-29).</summary>
    private async Task<string> GenerateOrderCodeAsync(DateTime now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = new string(Enumerable.Range(0, 4)
                .Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)])
                .ToArray());
            var code = $"DS{now:yyMMdd}{suffix}";
            if (!await _db.Orders.AnyAsync(o => o.OrderCode == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique order code.");
    }

    private static string VoucherMessage(string reason, Voucher voucher) => reason switch
    {
        VoucherIneligibility.Inactive => "This voucher is not active.",
        VoucherIneligibility.NotStarted => "This voucher is not available yet.",
        VoucherIneligibility.Expired => "This voucher has expired.",
        VoucherIneligibility.BelowMinimumOrder => $"This voucher needs a subtotal of at least {voucher.MinOrderAmount:N0} VND.",
        VoucherIneligibility.UsageLimitReached => "This voucher has been fully used.",
        _ => "This voucher cannot be applied.",
    };
}
