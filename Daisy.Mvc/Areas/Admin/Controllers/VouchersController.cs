using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-58 – UC-60 (Admin only).</summary>
[Authorize(Roles = "Admin")]
public sealed class VouchersController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index(string? state, int page = 1)
    {
        var query = $"?page={Math.Max(1, page)}&pageSize=20" + (string.IsNullOrEmpty(state) ? string.Empty : $"&state={state}");
        ViewData["Title"] = "Mã giảm giá";
        ViewData["State"] = state;
        return View(await admin.GetVouchersAsync(query));
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Tạo mã giảm giá";
        var now = DateTime.UtcNow.AddHours(7).Date;
        return View("Edit", new VoucherEditViewModel(null, null, new VoucherForm { StartAt = now, EndAt = now.AddDays(30).AddMinutes(-1) }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] VoucherForm form) => await SaveAsync(null, form);

    public async Task<IActionResult> Edit(int id)
    {
        var voucher = await admin.GetVoucherAsync(id);
        ViewData["Title"] = voucher.Code;
        return View(new VoucherEditViewModel(id, voucher, VoucherForm.From(voucher)));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] VoucherForm form) => await SaveAsync(id, form);

    [HttpPost]
    public Task<IActionResult> Active(int id, bool isActive) =>
        RunAsync(() => admin.SetVoucherActiveAsync(id, isActive), isActive ? "Đã bật mã giảm giá." : "Đã tắt mã giảm giá. Đơn đã dùng mã không bị ảnh hưởng.", "/admin/vouchers");

    public async Task<IActionResult> Usage(int id, int page = 1)
    {
        var usage = await admin.GetVoucherUsageAsync(id, Math.Max(1, page));
        ViewData["Title"] = $"Lượt dùng {usage.Voucher.Code}";
        return View(usage);
    }

    private async Task<IActionResult> SaveAsync(int? id, VoucherForm form)
    {
        if (form.StartAt is not null && form.EndAt is not null && form.StartAt >= form.EndAt)
        {
            ModelState.AddModelError("Form.EndAt", "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (form.DiscountType == Models.Api.DiscountType.Percent && form.DiscountValue > 100)
        {
            ModelState.AddModelError("Form.DiscountValue", "Giảm theo phần trăm tối đa 100.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (id is int existing)
                {
                    await admin.UpdateVoucherAsync(existing, form.ToRequest());
                }
                else
                {
                    await admin.CreateVoucherAsync(form.ToRequest());
                }

                Flash(id is null ? "Đã tạo mã giảm giá." : "Đã lưu mã giảm giá.");
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
            {
                AddApiError(ex, "Form");
            }
        }

        var voucher = id is int vid ? await admin.GetVoucherAsync(vid) : null;
        ViewData["Title"] = voucher?.Code ?? "Tạo mã giảm giá";
        return View("Edit", new VoucherEditViewModel(id, voucher, form));
    }
}
