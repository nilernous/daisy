using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers.Admin;

/// <summary>UC-67 – UC-70 (Admin only), definitions in BR-40.</summary>
[Route("api/admin/dashboard")]
[Authorize(Policy = Policies.Admin)]
public sealed class DashboardController : ApiControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<IReadOnlyList<RevenuePointDto>>> GetRevenue([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await _dashboard.GetRevenueAsync(query, ct));

    [HttpGet("orders-by-status")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusCountDto>>> GetOrdersByStatus([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await _dashboard.GetOrdersByStatusAsync(query, ct));

    [HttpGet("top-products")]
    public async Task<ActionResult<IReadOnlyList<TopProductDto>>> GetTopProducts([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await _dashboard.GetTopProductsAsync(query, ct));

    [HttpGet("new-customers")]
    public async Task<ActionResult<IReadOnlyList<NewCustomerPointDto>>> GetNewCustomers([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await _dashboard.GetNewCustomersAsync(query, ct));
}
