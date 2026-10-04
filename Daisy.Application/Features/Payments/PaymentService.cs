using System.Text.RegularExpressions;
using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Daisy.Application.Features.Payments;

public sealed partial class PaymentService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IDatabaseErrorClassifier _dbErrors;
    private readonly TimeProvider _time;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentSimulator? _simulator;

    public PaymentService(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        IDatabaseErrorClassifier dbErrors,
        TimeProvider time,
        ILogger<PaymentService> logger,
        IEnumerable<IPaymentSimulator> simulators)
    {
        _db = db;
        _gateway = gateway;
        _dbErrors = dbErrors;
        _time = time;
        _logger = logger;
        _simulator = simulators.FirstOrDefault();
    }

    /// <summary>OrderCode pattern from BR-29: DS + yyMMdd + 4 characters.</summary>
    [GeneratedRegex("DS[0-9]{6}[A-Z0-9]{4}")]
    private static partial Regex OrderCodeRegex();

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonAlphanumericRegex();

    // ---- Customer: VietQR (UC-35, UC-38) -------------------------------------------

    public async Task<PaymentQrDto> GetPaymentQrAsync(int userId, int orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.PaymentMethod != PaymentMethod.Online
            || order.PaymentStatus != PaymentStatus.Unpaid
            || order.OrderStatus == OrderStatus.Canceled)
        {
            throw new ConflictException(ErrorCodes.PaymentNotAvailable, "This order does not need a VietQR payment.");
        }

        if (!order.CanPayWithQr(_time.GetVietnamNow()))
        {
            throw new ConflictException(
                ErrorCodes.PaymentExpired,
                "The payment time for this order has expired.",
                new { expiredAt = order.PaymentExpiresAt });
        }

        var qr = await _gateway.CreatePaymentAsync(new PaymentRequest(order.OrderCode, order.TotalAmount), ct);
        return new PaymentQrDto(
            order.OrderId, order.OrderCode, qr.Amount, qr.BankBin, qr.BankName, qr.AccountNumber,
            qr.AccountName, qr.TransferContent, qr.QrImageUrl, order.PaymentExpiresAt);
    }

    // ---- Webhook (UC-36, BR-32, BR-33, BR-42) ----------------------------------------

    public async Task<WebhookAckDto> HandleWebhookAsync(PaymentWebhookRequest request, CancellationToken ct)
    {
        var result = await _gateway.VerifyWebhookAsync(request, ct);
        if (!result.IsValid)
        {
            throw new UnauthorizedException(ErrorCodes.InvalidWebhookSignature, "The webhook could not be verified.");
        }

        var transfer = result.Transfer;
        if (transfer is null || !transfer.IsIncoming)
        {
            return new WebhookAckDto(true);
        }

        if (await _db.PaymentTransactions.AnyAsync(t => t.BankReference == transfer.Reference, ct))
        {
            return new WebhookAckDto(true);
        }

        var order = await FindOrderFromContentAsync(transfer.Content, ct);
        var match = Decide(order, transfer);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        if (match == PaymentMatchResult.Matched && order!.TransactionCode != transfer.Reference)
        {
            var codeUsedElsewhere = await _db.Orders.AnyAsync(o => o.TransactionCode == transfer.Reference && o.OrderId != order.OrderId, ct);
            var updated = codeUsedElsewhere ? 0 : await MarkPaidAsync(order.OrderId, transfer.TransferredAt, transfer.Reference, ct);
            if (updated == 0)
            {
                match = PaymentMatchResult.OrderNotPayable;
            }
        }

        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            BankReference = transfer.Reference,
            Provider = _gateway.ProviderName,
            AccountNumber = transfer.AccountNumber,
            Amount = transfer.Amount,
            TransferContent = transfer.Content is { Length: > 500 } c ? c[..500] : transfer.Content,
            TransferredAt = transfer.TransferredAt,
            ReceivedAt = _time.GetVietnamNow(),
            OrderId = order?.OrderId,
            MatchResult = match,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (_dbErrors.IsUniqueConstraintViolation(ex))
        {
            // The same webhook was delivered twice at the same moment; the other delivery wins.
            await transaction.RollbackAsync(ct);
            return new WebhookAckDto(true);
        }

        await transaction.CommitAsync(ct);
        if (match != PaymentMatchResult.Matched)
        {
            _logger.LogWarning(
                "Bank transfer {Reference} of {Amount} was not matched ({MatchResult}); it waits for staff review.",
                transfer.Reference, transfer.Amount, match);
        }

        return new WebhookAckDto(true);
    }

    /// <summary>Development only (Mock provider): runs a simulated transfer through the webhook flow.</summary>
    public async Task<WebhookAckDto> SimulateTransferAsync(SimulateTransferRequest request, CancellationToken ct)
    {
        if (_simulator is null)
        {
            throw new NotFoundException("Payment simulation is only available with the Mock provider.");
        }

        var code = request.OrderCode.Trim().ToUpperInvariant();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderCode == code, ct)
            ?? throw new NotFoundException("Order not found.");
        var reference = $"MOCK{_time.GetUtcNow():yyMMddHHmmssfff}";
        var webhook = _simulator.BuildWebhook(reference, request.Amount ?? order.TotalAmount, request.Content ?? code);
        return await HandleWebhookAsync(webhook, ct);
    }

    // ---- Staff: payment status (UC-57, BR-34) ---------------------------------------------

    public static IReadOnlyList<PaymentAction> AllowedActions(Order order)
    {
        var actions = new List<PaymentAction>();
        if (order.PaymentMethod == PaymentMethod.COD && order.PaymentStatus == PaymentStatus.Unpaid && order.OrderStatus != OrderStatus.Canceled)
        {
            actions.Add(PaymentAction.ConfirmCod);
        }

        if (order.CanAcceptTransfer)
        {
            actions.Add(PaymentAction.ConfirmTransfer);
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            actions.Add(PaymentAction.Refund);
        }

        return actions;
    }

    public async Task UpdatePaymentAsync(int orderId, UpdatePaymentRequest request, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
            ?? throw new NotFoundException("Order not found.");
        var now = _time.GetVietnamNow();

        if (!AllowedActions(order).Contains(request.Action))
        {
            throw new ConflictException(ErrorCodes.PaymentActionNotAllowed, "This payment action is not allowed for this order.");
        }

        int updated;
        switch (request.Action)
        {
            case PaymentAction.ConfirmCod:
                updated = await _db.Orders
                    .Where(o => o.OrderId == orderId && o.PaymentMethod == PaymentMethod.COD
                        && o.PaymentStatus == PaymentStatus.Unpaid && o.OrderStatus != OrderStatus.Canceled)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                        .SetProperty(o => o.PaidAt, now), ct);
                break;

            case PaymentAction.ConfirmTransfer:
                var reference = request.TransactionCode?.Trim();
                if (string.IsNullOrEmpty(reference) || reference.Length > 100)
                {
                    throw new ValidationException([new ValidationFailure("transactionCode", "Enter the bank reference (at most 100 characters).")]);
                }

                if (await _db.PaymentTransactions.AnyAsync(t => t.BankReference == reference, ct))
                {
                    throw new ConflictException(
                        ErrorCodes.TransactionRecorded,
                        "This transfer was reported by the bank. Resolve it in the unmatched transfers list.");
                }

                if (await _db.Orders.AnyAsync(o => o.TransactionCode == reference, ct))
                {
                    throw new ConflictException(ErrorCodes.TransactionCodeUsed, "This bank reference is already used by another order.");
                }

                updated = await MarkPaidAsync(orderId, now, reference, ct);
                break;

            case PaymentAction.Refund:
                updated = await _db.Orders
                    .Where(o => o.OrderId == orderId && o.PaymentStatus == PaymentStatus.Paid)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.Refunded), ct);
                break;

            default:
                throw new ValidationException([new ValidationFailure("action", "Unknown payment action.")]);
        }

        if (updated == 0)
        {
            throw new ConflictException(ErrorCodes.PaymentActionNotAllowed, "The order changed meanwhile. Reload it and try again.");
        }
    }

    // ---- Staff: reconciliation (UC-71, BR-42) ----------------------------------------------

    public async Task<PagedResult<PaymentTransactionDto>> GetTransactionsAsync(PaymentTransactionQuery query, CancellationToken ct)
    {
        query.ValidatePage();
        var transactions = _db.PaymentTransactions.AsNoTracking();

        if (query.NeedsReview is bool needsReview)
        {
            transactions = needsReview
                ? transactions.Where(t => t.MatchResult != PaymentMatchResult.Matched && t.Resolution == null)
                : transactions.Where(t => t.MatchResult == PaymentMatchResult.Matched || t.Resolution != null);
        }

        if (query.MatchResult is PaymentMatchResult matchResult)
        {
            transactions = transactions.Where(t => t.MatchResult == matchResult);
        }

        if (query.Resolution is PaymentResolution resolution)
        {
            transactions = transactions.Where(t => t.Resolution == resolution);
        }

        if (query.From is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            transactions = transactions.Where(t => t.TransferredAt >= start);
        }

        if (query.To is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            transactions = transactions.Where(t => t.TransferredAt < end);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            transactions = transactions.Where(t => t.BankReference.Contains(term) || (t.TransferContent != null && t.TransferContent.Contains(term)));
        }

        var page = await transactions
            .Include(t => t.Order)
            .Include(t => t.ResolvedBy)
            .OrderByDescending(t => t.ReceivedAt).ThenByDescending(t => t.PaymentTransactionId)
            .ToPagedResultAsync(query, ct);
        return page.Map(ToDto);
    }

    /// <summary>Applies an unmatched transfer to an order, which becomes Paid.</summary>
    public async Task<PaymentTransactionDto> ApplyTransactionAsync(int staffUserId, int transactionId, ApplyTransactionRequest request, CancellationToken ct)
    {
        if (request.Note is { Length: > 500 })
        {
            throw new ValidationException([new ValidationFailure("note", "Note must be at most 500 characters.")]);
        }

        var transfer = await FindTransactionAsync(transactionId, ct);
        if (!transfer.NeedsReview)
        {
            throw new ConflictException(ErrorCodes.TransactionAlreadyResolved, "This transfer is already resolved.");
        }

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == request.OrderId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (!order.CanAcceptTransfer || order.TotalAmount > transfer.Amount)
        {
            throw new ConflictException(
                ErrorCodes.OrderNotPayable,
                "This order cannot take this transfer: it must be an unpaid VietQR order whose total is at most the transfer amount.");
        }

        if (await _db.Orders.AnyAsync(o => o.TransactionCode == transfer.BankReference, ct))
        {
            throw new ConflictException(ErrorCodes.TransactionCodeUsed, "This bank reference is already used by another order.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        if (await MarkPaidAsync(order.OrderId, transfer.TransferredAt, transfer.BankReference, ct) == 0)
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException(ErrorCodes.OrderNotPayable, "The order changed meanwhile. Reload it and try again.");
        }

        transfer.Resolve(PaymentResolution.Applied, staffUserId, ValidationRules.TrimToNull(request.Note), _time.GetVietnamNow(), order.OrderId);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetTransactionDtoAsync(transactionId, ct);
    }

    /// <summary>Marks an unmatched transfer Refunded or Ignored; the resolution is final.</summary>
    public async Task<PaymentTransactionDto> ResolveTransactionAsync(int staffUserId, int transactionId, ResolveTransactionRequest request, CancellationToken ct)
    {
        var errors = new List<ValidationFailure>();
        if (request.Resolution is not (PaymentResolution.Refunded or PaymentResolution.Ignored))
        {
            errors.Add(new("resolution", "Resolution must be Refunded or Ignored; use apply to attach a transfer to an order."));
        }

        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 500)
        {
            errors.Add(new("note", "A note of at most 500 characters is required."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var transfer = await FindTransactionAsync(transactionId, ct);
        transfer.Resolve(request.Resolution, staffUserId, request.Note.Trim(), _time.GetVietnamNow());
        await _db.SaveChangesAsync(ct);
        return await GetTransactionDtoAsync(transactionId, ct);
    }

    // ---- Helpers ---------------------------------------------------------------------------

    /// <summary>
    /// Marks an order Paid only while it is an unpaid, non-canceled VietQR order. The opposite
    /// condition of the expiry job, so exactly one of them wins (BR-33, BR-41).
    /// </summary>
    private Task<int> MarkPaidAsync(int orderId, DateTime paidAt, string reference, CancellationToken ct) =>
        _db.Orders
            .Where(o => o.OrderId == orderId
                && o.PaymentMethod == PaymentMethod.Online
                && o.PaymentStatus == PaymentStatus.Unpaid
                && o.OrderStatus != OrderStatus.Canceled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(o => o.PaidAt, paidAt)
                .SetProperty(o => o.TransactionCode, reference), ct);

    private async Task<Order?> FindOrderFromContentAsync(string? content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        // Banks may change case, add spaces or punctuation, so compare letters and digits only.
        var normalized = NonAlphanumericRegex().Replace(content.ToUpperInvariant(), string.Empty);
        var candidates = OrderCodeRegex().Matches(normalized).Select(m => m.Value).Distinct().ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var orders = await _db.Orders.AsNoTracking().Where(o => candidates.Contains(o.OrderCode)).ToListAsync(ct);
        return candidates.Select(c => orders.FirstOrDefault(o => o.OrderCode == c)).FirstOrDefault(o => o is not null);
    }

    private static PaymentMatchResult Decide(Order? order, BankTransfer transfer)
    {
        if (order is null)
        {
            return PaymentMatchResult.NoOrder;
        }

        if (order.TransactionCode == transfer.Reference)
        {
            // Staff already confirmed this transfer manually before the webhook arrived.
            return PaymentMatchResult.Matched;
        }

        if (!order.CanAcceptTransfer)
        {
            return PaymentMatchResult.OrderNotPayable;
        }

        return transfer.Amount < order.TotalAmount ? PaymentMatchResult.Underpaid : PaymentMatchResult.Matched;
    }

    private async Task<PaymentTransaction> FindTransactionAsync(int id, CancellationToken ct) =>
        await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.PaymentTransactionId == id, ct)
        ?? throw new NotFoundException("Transfer not found.");

    private async Task<PaymentTransactionDto> GetTransactionDtoAsync(int id, CancellationToken ct)
    {
        var transfer = await _db.PaymentTransactions.AsNoTracking()
            .Include(t => t.Order)
            .Include(t => t.ResolvedBy)
            .FirstAsync(t => t.PaymentTransactionId == id, ct);
        return ToDto(transfer);
    }

    private static PaymentTransactionDto ToDto(PaymentTransaction t) =>
        new(
            t.PaymentTransactionId,
            t.BankReference,
            t.Provider,
            t.AccountNumber,
            t.Amount,
            t.TransferContent,
            t.TransferredAt,
            t.ReceivedAt,
            t.MatchResult,
            t.Order is null ? null : new PaymentTransactionOrderDto(t.Order.OrderId, t.Order.OrderCode, t.Order.OrderStatus, t.Order.PaymentStatus, t.Order.TotalAmount),
            t.NeedsReview,
            t.Resolution,
            t.ResolutionNote,
            t.ResolvedBy is null ? null : new ResolvedByDto(t.ResolvedBy.UserId, t.ResolvedBy.FullName),
            t.ResolvedAt);
}
