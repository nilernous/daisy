using Daisy.Mvc.Infrastructure;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Controllers;

/// <summary>UC-08 – UC-12.</summary>
[Route("profile")]
[Authorize(Roles = "Customer")]
public sealed class ProfileController(ProfileApiService profile) : WebController
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => await ShowAsync(null, null);

    [HttpPost("")]
    public async Task<IActionResult> Update([Bind(Prefix = "Form")] ProfileForm form)
    {
        if (form.Avatar is { Length: > 2 * 1024 * 1024 })
        {
            ModelState.AddModelError("Form.Avatar", "Ảnh đại diện tối đa 2 MB.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await profile.UpdateProfileAsync(form.FullName.Trim(), form.Phone, form.Gender, form.DateOfBirth, form.Avatar, form.RemoveAvatar);
                Flash("Đã lưu thông tin cá nhân.");
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.BadRequest)
            {
                AddApiError(ex, "Form");
            }
        }

        return await ShowAsync(form, null);
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([Bind(Prefix = "NewAddress")] AddressForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await profile.AddAddressAsync(form.ToRequest());
                Flash("Đã thêm địa chỉ.");
                return RedirectToLocal(form.ReturnUrl, nameof(Index), "Profile");
            }
            catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.BadRequest)
            {
                AddApiError(ex, "NewAddress");
            }
        }

        return await ShowAsync(null, form);
    }

    [HttpGet("addresses/{id:int}/edit")]
    public async Task<IActionResult> EditAddress(int id)
    {
        var address = (await profile.GetAddressesAsync()).FirstOrDefault(a => a.Id == id);
        if (address is null)
        {
            return View("NotFound");
        }

        ViewData["Title"] = "Sửa địa chỉ";
        ViewData["AddressId"] = id;
        return View(AddressForm.From(address));
    }

    [HttpPost("addresses/{id:int}/edit")]
    public async Task<IActionResult> EditAddress(int id, AddressForm form)
    {
        ViewData["Title"] = "Sửa địa chỉ";
        ViewData["AddressId"] = id;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await profile.UpdateAddressAsync(id, form.ToRequest());
            Flash("Đã cập nhật địa chỉ.");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.BadRequest)
        {
            AddApiError(ex);
            return View(form);
        }
    }

    [HttpPost("addresses/{id:int}/delete")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        await profile.DeleteAddressAsync(id);
        Flash("Đã xóa địa chỉ.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("addresses/{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        await profile.SetDefaultAddressAsync(id);
        Flash("Đã đặt làm địa chỉ mặc định.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ShowAsync(ProfileForm? form, AddressForm? newAddress)
    {
        var me = await profile.GetProfileAsync();
        var addresses = await profile.GetAddressesAsync();
        form ??= new ProfileForm
        {
            FullName = me.FullName,
            Phone = me.Phone,
            Gender = me.Gender,
            DateOfBirth = me.DateOfBirth,
        };
        newAddress ??= new AddressForm { ReceiverName = me.FullName, ReceiverPhone = me.Phone ?? string.Empty };
        ViewData["Title"] = "Hồ sơ của tôi";
        return View("Index", new ProfileViewModel(me, addresses, form, newAddress));
    }
}
