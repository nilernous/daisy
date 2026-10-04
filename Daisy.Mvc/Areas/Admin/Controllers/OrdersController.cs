using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-53 – UC-57.</summary>
public sealed class OrdersController(AdminApiService admin) : AdminController
{
    private const int PageSize = 20;

    public async Task<IActionResult> Index([FromQuery] AdminOrderFilter filter)
    {
        var page = await admin.QueryOrdersAsync(filter.ToOData(PageSize));
        var total = page.Count ?? page.Value.Count;
        ViewData["Title"] = "Đơn hàng";
        return View(new OrdersAdminViewModel(filter, page.Value, total, (int)Math.Ceiling(total / (double)PageSize)));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var order = await admin.GetOrderAsync(id);
        ViewData["Title"] = $"Đơn {order.OrderCode}";
        return View(order);
    }

    [HttpPost]
    public Task<IActionResult> Status(int id, OrderStatus status) =>
        RunAsync(() => admin.UpdateOrderStatusAsync(id, status), $"Đã chuyển đơn sang “{Infrastructure.Display.Label(status)}”.", $"/admin/orders/detail/{id}");

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Flash("Nhập lý do hủy đơn.", "bad");
            return Redirect($"/admin/orders/detail/{id}");
        }

        try
        {
            var result = await admin.CancelOrderAsync(id, reason.Trim());
            Flash(result.RefundRequired ? "Đã hủy đơn và hoàn lại tồn kho. Đơn đã thanh toán: hãy hoàn tiền cho khách rồi đánh dấu Đã hoàn tiền." : "Đã hủy đơn và hoàn lại tồn kho.");
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest)
        {
            Flash(Infrastructure.ErrorMessages.For(ex), "bad");
        }

        return Redirect($"/admin/orders/detail/{id}");
    }

    [HttpPost]
    public Task<IActionResult> Payment(int id, string paymentAction, string? transactionCode) =>
        RunAsync(() => admin.UpdatePaymentAsync(id, paymentAction, transactionCode), "Đã cập nhật trạng thái thanh toán.", $"/admin/orders/detail/{id}");
}
