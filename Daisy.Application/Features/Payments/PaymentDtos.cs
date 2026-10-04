using Daisy.Application.Common;
using Daisy.Domain.Enums;

namespace Daisy.Application.Features.Payments;

/// <summary>Staff payment actions (UC-57).</summary>
public enum PaymentAction
{
    ConfirmCod,
    ConfirmTransfer,
    Refund
}

public sealed record PaymentQrDto(
    int OrderId,
    string OrderCode,
    decimal Amount,
    string BankBin,
    string BankName,
    string AccountNumber,
    string AccountName,
    string TransferContent,
    string QrImageUrl,
    DateTime? ExpiresAt);

public sealed record UpdatePaymentRequest(PaymentAction Action, string? TransactionCode);

public sealed record WebhookAckDto(bool Success);

public sealed record PaymentTransactionOrderDto(
    int Id,
    string OrderCode,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    decimal TotalAmount);

public sealed record ResolvedByDto(int Id, string FullName);

public sealed record PaymentTransactionDto(
    int Id,
    string BankReference,
    string Provider,
    string AccountNumber,
    decimal Amount,
    string? TransferContent,
    DateTime TransferredAt,
    DateTime ReceivedAt,
    PaymentMatchResult MatchResult,
    PaymentTransactionOrderDto? Order,
    bool NeedsReview,
    PaymentResolution? Resolution,
    string? ResolutionNote,
    ResolvedByDto? ResolvedBy,
    DateTime? ResolvedAt);

public sealed record PaymentTransactionQuery : PageRequest
{
    public bool? NeedsReview { get; init; }

    public PaymentMatchResult? MatchResult { get; init; }

    public PaymentResolution? Resolution { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public string? Search { get; init; }
}

public sealed record ApplyTransactionRequest(int OrderId, string? Note);

public sealed record ResolveTransactionRequest(PaymentResolution Resolution, string Note);

/// <summary>Development only: simulates a bank transfer for an order (Mock provider).</summary>
public sealed record SimulateTransferRequest(string OrderCode, decimal? Amount, string? Content);
