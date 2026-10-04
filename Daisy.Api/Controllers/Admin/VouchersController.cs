using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-58 – UC-60 (Admin only).</summary>
[Route("api/admin/vouchers")]
[Authorize(Policy = Policies.Admin)]
public sealed class VouchersController : ApiControllerBase
{
    private readonly VoucherService _vouchers;

    public VouchersController(VoucherService vouchers)
    {
        _vouchers = vouchers;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<VoucherDto>>> GetVouchers([FromQuery] VoucherQuery query, CancellationToken ct) =>
        await _vouchers.GetListAsync(query, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VoucherDto>> GetVoucher(int id, CancellationToken ct) =>
        await _vouchers.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> CreateVoucher(VoucherRequest request, CancellationToken ct)
    {
        var voucher = await _vouchers.CreateAsync(request, ct);
        return Created($"/api/admin/vouchers/{voucher.Id}", voucher);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VoucherDto>> UpdateVoucher(int id, VoucherRequest request, CancellationToken ct) =>
        await _vouchers.UpdateAsync(id, request, ct);

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetVoucherActive(int id, SetActiveRequest request, CancellationToken ct)
    {
        await _vouchers.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/usage")]
    public async Task<ActionResult<VoucherUsageDto>> GetVoucherUsage(int id, [FromQuery] PageRequest page, CancellationToken ct) =>
        await _vouchers.GetUsageAsync(id, page, ct);
}
