using Daisy.Api.Infrastructure;
using Daisy.Application.Features.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Daisy.Api.OData;

/// <summary>GET /odata/orders — UC-53.</summary>
[Authorize(Policy = Policies.Staff)]
public sealed class OrdersController : ODataController
{
    private readonly OrderService _orders;

    public OrdersController(OrderService orders)
    {
        _orders = orders;
    }

    [DaisyEnableQuery]
    public IQueryable<OrderAdminListDto> Get() => _orders.QueryAdminList();
}
