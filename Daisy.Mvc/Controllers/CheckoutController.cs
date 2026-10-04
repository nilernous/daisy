using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-32 – UC-35. The backend calculates every amount (BR-24).</summary>
[Route("checkout")]
[Authorize(Roles = "Customer")]
public sealed class CheckoutController(OrderApiService orders) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? voucher, int? addressId, PaymentMethod? method)
    {
        var checkout = await orders.GetCheckoutAsync();
        if (checkout.Items.Count == 0)
        {
            Flash("Chọn sản phẩm trong giỏ trước khi thanh toán.", "info");
            return RedirectToAction("Index", "Cart");
        }

        var (totals, voucherError) = await PreviewAsync(checkout, voucher);
        var form = new PlaceOrderForm
        {
            AddressId = addressId ?? checkout.DefaultAddressId ?? checkout.Addresses.FirstOrDefault()?.Id,
            PaymentMethod = method ?? PaymentMethod.COD,
            VoucherCode = voucherError is null ? totals.Voucher?.Code : null,
            ExpectedTotal = totals.TotalAmount,
        };
        ViewData["Title"] = "Thanh toán";
        return View(new CheckoutViewModel(checkout, totals, form.VoucherCode, voucherError, form));
    }

    [HttpPost("")]
    public async Task<IActionResult> PlaceOrder(PlaceOrderForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var placed = await orders.PlaceOrderAsync(form.AddressId!.Value, form.PaymentMethod, form.VoucherCode, form.CustomerNote, form.ExpectedTotal);
                if (placed.PaymentMethod == PaymentMethod.Online)
                {
                    return Redirect($"/orders/{placed.OrderId}/payment");
                }

                Flash($"Đặt hàng thành công. Mã đơn của bạn là {placed.OrderCode}.");
                return Redirect($"/orders/{placed.OrderId}");
            }
            catch (Services.ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
            {
                AddApiError(ex);
            }
        }

        var checkout = await orders.GetCheckoutAsync();
        var (totals, voucherError) = await PreviewAsync(checkout, form.VoucherCode);
        form.ExpectedTotal = totals.TotalAmount;
        ViewData["Title"] = "Thanh toán";
        return View("Index", new CheckoutViewModel(checkout, totals, voucherError is null ? form.VoucherCode : null, voucherError, form));
    }

    private async Task<(CheckoutTotals Totals, string? Error)> PreviewAsync(Checkout checkout, string? voucher)
    {
        if (string.IsNullOrWhiteSpace(voucher))
        {
            return (checkout.Totals, null);
        }

        try
        {
            return (await orders.PreviewVoucherAsync(voucher.Trim()), null);
        }
        catch (Services.ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.TooManyRequests)
        {
            return (checkout.Totals, ErrorMessages.For(ex));
        }
    }
}
