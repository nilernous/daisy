using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-71 Reconcile Payment Transactions (BR-42).</summary>
public sealed class PaymentsController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index(bool needsReview = true, int page = 1)
    {
        var query = $"?page={Math.Max(1, page)}&pageSize=20" + (needsReview ? "&needsReview=true" : string.Empty);
        ViewData["Title"] = "Đối soát chuyển khoản";
        return View(new TransactionsViewModel(await admin.GetTransactionsAsync(query), needsReview, page));
    }

    /// <summary>Staff type the order code; the order id is looked up with OData.</summary>
    [HttpPost]
    public async Task<IActionResult> Apply(int id, string? orderCode, string? note)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            Flash("Nhập mã đơn hàng cần gán.", "bad");
            return RedirectToAction(nameof(Index));
        }

        var code = orderCode.Trim().ToUpperInvariant();
        var match = await admin.QueryOrdersAsync(new ODataQuery().Filter($"orderCode eq {ODataQuery.Literal(code)}").Top(1).Select("id,orderCode"));
        if (match.Value.Count == 0)
        {
            Flash($"Không tìm thấy đơn {code}.", "bad");
            return RedirectToAction(nameof(Index));
        }

        return await RunAsync(() => admin.ApplyTransactionAsync(id, match.Value[0].Id, note), $"Đã gán giao dịch vào đơn {code}; đơn đã chuyển sang Đã thanh toán.", "/admin/payments");
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(int id, PaymentResolution resolution, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            Flash("Ghi chú lý do trước khi đóng giao dịch.", "bad");
            return RedirectToAction(nameof(Index));
        }

        return await RunAsync(() => admin.ResolveTransactionAsync(id, resolution, note.Trim()), resolution == PaymentResolution.Refunded ? "Đã ghi nhận hoàn tiền." : "Đã bỏ qua giao dịch.", "/admin/payments");
    }
}
