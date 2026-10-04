using Daisy.Mvc.Models.Api;
using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-63 – UC-66 (Admin only).</summary>
[Authorize(Roles = "Admin")]
public sealed class UsersController(AdminApiService admin) : AdminController
{
    private const int PageSize = 20;

    public async Task<IActionResult> Index(string? q, UserRole? role, UserStatus? status, int page = 1)
    {
        var query = new ODataQuery().OrderBy("createdAt desc").Page(page, PageSize);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = ODataQuery.Literal(q.Trim().ToLowerInvariant());
            query.Filter($"contains(tolower(fullName),{term}) or contains(tolower(email),{term})");
        }

        if (role is not null) query.Filter($"role eq '{role}'");
        if (status is not null) query.Filter($"status eq '{status}'");

        var result = await admin.QueryUsersAsync(query);
        var total = result.Count ?? result.Value.Count;
        ViewData["Title"] = "Người dùng";
        return View(new UsersAdminViewModel(result.Value, total, page, (int)Math.Ceiling(total / (double)PageSize), q, role, status));
    }

    public async Task<IActionResult> Detail(int id, int page = 1)
    {
        var user = await admin.GetUserAsync(id, Math.Max(1, page));
        ViewData["Title"] = user.FullName;
        return View(user);
    }

    [HttpPost]
    public Task<IActionResult> Status(int id, UserStatus status) =>
        RunAsync(() => admin.SetUserStatusAsync(id, status), status == UserStatus.Locked ? "Đã khóa tài khoản và đăng xuất người dùng." : "Đã mở khóa tài khoản.", $"/admin/users/detail/{id}");

    [HttpPost]
    public Task<IActionResult> Role(int id, UserRole role) =>
        RunAsync(() => admin.SetUserRoleAsync(id, role), $"Đã đổi vai trò thành {Infrastructure.Display.Label(role)}. Áp dụng từ lần làm mới phiên đăng nhập kế tiếp.", $"/admin/users/detail/{id}");
}
