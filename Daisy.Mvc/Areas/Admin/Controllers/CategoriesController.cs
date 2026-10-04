using Daisy.Mvc.Services;
using Daisy.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>UC-43, UC-44.</summary>
public sealed class CategoriesController(AdminApiService admin) : AdminController
{
    public async Task<IActionResult> Index(int? edit)
    {
        var tree = await admin.GetCategoriesAsync();
        var form = new CategoryForm();
        if (edit is int id)
        {
            var category = await admin.GetCategoryAsync(id);
            form = new CategoryForm
            {
                Name = category.Name,
                Slug = category.Slug,
                ParentCategoryId = category.ParentCategoryId,
                ImageUrl = category.ImageUrl,
                SortOrder = category.SortOrder,
            };
        }

        ViewData["Title"] = "Danh mục";
        return View(new CategoriesViewModel(tree, form, edit));
    }

    [HttpPost]
    public async Task<IActionResult> Save(int? id, CategoryForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (id is int existing)
                {
                    await admin.UpdateCategoryAsync(existing, form.ToRequest());
                    Flash("Đã lưu danh mục.");
                }
                else
                {
                    await admin.CreateCategoryAsync(form.ToRequest());
                    Flash("Đã thêm danh mục.");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
            {
                AddApiError(ex, "Form");
            }
        }

        ViewData["Title"] = "Danh mục";
        return View("Index", new CategoriesViewModel(await admin.GetCategoriesAsync(), form, id));
    }

    [HttpPost]
    public Task<IActionResult> Visibility(int id, bool isActive) =>
        RunAsync(() => admin.SetCategoryVisibilityAsync(id, isActive), isActive ? "Đã hiện danh mục." : "Đã ẩn danh mục và các danh mục con khỏi cửa hàng.", "/admin/categories");
}
