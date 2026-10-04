using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-71 Reconcile Payment Transactions (BR-42).</summary>
[Route("api/admin/payment-transactions")]
[Authorize(Policy = Policies.Staff)]
public sealed class PaymentTransactionsController : ApiControllerBase
{
    private readonly PaymentService _payments;

    public PaymentTransactionsController(PaymentService payments)
    {
        _payments = payments;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentTransactionDto>>> GetTransactions([FromQuery] PaymentTransactionQuery query, CancellationToken ct) =>
        await _payments.GetTransactionsAsync(query, ct);

    [HttpPost("{id:int}/apply")]
    public async Task<ActionResult<PaymentTransactionDto>> ApplyTransaction(int id, ApplyTransactionRequest request, CancellationToken ct) =>
        await _payments.ApplyTransactionAsync(CurrentUserId, id, request, ct);

    [HttpPatch("{id:int}/resolution")]
    public async Task<ActionResult<PaymentTransactionDto>> ResolveTransaction(int id, ResolveTransactionRequest request, CancellationToken ct) =>
        await _payments.ResolveTransactionAsync(CurrentUserId, id, request, ct);
}
