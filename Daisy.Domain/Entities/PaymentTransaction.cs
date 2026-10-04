using Daisy.Domain.Common;
using Daisy.Domain.Enums;

namespace Daisy.Domain.Entities;

/// <summary>A verified incoming bank transfer reported by the webhook service (BR-42).</summary>
public class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }

    public string BankReference { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? TransferContent { get; set; }

    public DateTime TransferredAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public int? OrderId { get; set; }

    public PaymentMatchResult MatchResult { get; set; }

    public PaymentResolution? Resolution { get; private set; }

    public string? ResolutionNote { get; private set; }

    public int? ResolvedByUserId { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    public Order? Order { get; set; }

    public User? ResolvedBy { get; set; }

    public bool NeedsReview => MatchResult != PaymentMatchResult.Matched && Resolution is null;

    /// <summary>A transfer is resolved once and the resolution is final (BR-42).</summary>
    public void Resolve(PaymentResolution resolution, int resolvedByUserId, string? note, DateTime now, int? orderId = null)
    {
        if (!NeedsReview)
        {
            throw new DomainException(ErrorCodes.TransactionAlreadyResolved, "This transfer is already resolved.");
        }

        if (resolution == PaymentResolution.Applied && orderId is null)
        {
            throw new InvalidOperationException("An applied transfer needs an order.");
        }

        Resolution = resolution;
        ResolutionNote = note;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = now;
        if (orderId is not null)
        {
            OrderId = orderId;
        }
    }
}
