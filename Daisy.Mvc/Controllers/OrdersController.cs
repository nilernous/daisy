using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-35 payment page, UC-37 – UC-41.</summary>
[Route("orders")]
[Authorize(Roles = "Customer")]
public sealed class OrdersController(OrderApiService orders, PaymentApiService payments, IWebHostEnvironment environment) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, OrderStatus? status = null)
    {
        ViewData["Title"] = "Đơn hàng của tôi";
        return View(new OrdersViewModel(await orders.GetOrdersAsync(Math.Max(1, page), status), status));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var order = await orders.GetOrderAsync(id);
        ViewData["Title"] = $"Đơn hàng {order.OrderCode}";
        return View(order);
    }

    /// <summary>UC-35: VietQR code with a countdown to the payment deadline (BR-41).</summary>
    [HttpGet("{id:int}/payment")]
    public async Task<IActionResult> Payment(int id)
    {
        var order = await orders.GetOrderAsync(id);
        if (!order.CanPayWithQr)
        {
            return Redirect($"/orders/{id}");
        }

        try
        {
            var qr = await payments.GetQrAsync(id);
            ViewData["Title"] = $"Thanh toán đơn {order.OrderCode}";
            return View(new PaymentViewModel(order, qr, environment.IsDevelopment()));
        }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.Conflict)
        {
            Flash(ErrorMessages.For(ex), "bad");
            return Redirect($"/orders/{id}");
        }
    }

    /// <summary>Polled by the payment page every few seconds.</summary>
    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> Status(int id)
    {
        var order = await orders.GetOrderAsync(id);
        return Json(new { paymentStatus = order.PaymentStatus.ToString(), orderStatus = order.OrderStatus.ToString() });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Flash("Hãy cho chúng tôi biết lý do hủy đơn.", "bad");
            return Redirect($"/orders/{id}");
        }

        try
        {
            var result = await orders.CancelAsync(id, reason.Trim());
            Flash(result.RefundRequired
                ? "Đã hủy đơn hàng. Cửa hàng sẽ liên hệ để hoàn tiền cho bạn."
                : "Đã hủy đơn hàng.");
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest)
        {
            Flash(ErrorMessages.For(ex), "bad");
        }

        return Redirect($"/orders/{id}");
    }

    /// <summary>UC-41.</summary>
    [HttpPost("{id:int}/items/{itemId:int}/review")]
    public async Task<IActionResult> Review(int id, int itemId, int rating, string? comment)
    {
        try
        {
            await orders.ReviewAsync(itemId, rating, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim());
            Flash("Cảm ơn bạn đã đánh giá sản phẩm.");
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest)
        {
            Flash(ex.Code == "VALIDATION_FAILED" ? "Chọn từ 1 đến 5 sao; nhận xét tối đa 1000 ký tự." : ErrorMessages.For(ex), "bad");
        }

        return Redirect($"/orders/{id}#items");
    }

    /// <summary>Development only: simulates the customer's bank transfer (Mock provider).</summary>
    [HttpPost("{id:int}/simulate-transfer")]
    public async Task<IActionResult> SimulateTransfer(int id)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var order = await orders.GetOrderAsync(id);
        await payments.SimulateTransferAsync(order.OrderCode);
        return Redirect($"/orders/{id}/payment");
    }
}
