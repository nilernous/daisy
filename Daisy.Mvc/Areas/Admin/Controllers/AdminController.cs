using Daisy.Mvc.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Mvc.Areas.Admin.Controllers;

/// <summary>Base for the admin panel: Staff and Admin only (BR-07). Admin-only pages narrow the role.</summary>
[Area("Admin")]
[Authorize(Roles = "Staff,Admin")]
public abstract class AdminController : WebController
{
    protected bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Runs an API call from a button form and reports the result as a flash message.</summary>
    protected async Task<IActionResult> RunAsync(Func<Task> action, string success, string redirectUrl)
    {
        try
        {
            await action();
            Flash(success);
        }
        catch (Services.ApiException ex) when (ex.Status is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
        {
            Flash(ErrorMessages.For(ex), "bad");
        }

        return Redirect(redirectUrl);
    }
}
