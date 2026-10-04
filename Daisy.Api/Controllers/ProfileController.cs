using Daisy.Api.Infrastructure;
using Daisy.Application.Common;
using Daisy.Application.Features.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daisy.Api.Controllers;

/// <summary>UC-08 – UC-12.</summary>
[Route("api/profile")]
[Authorize(Policy = Policies.Customer)]
public sealed class ProfileController : ApiControllerBase
{
    private readonly ProfileService _profile;

    public ProfileController(ProfileService profile)
    {
        _profile = profile;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken ct) =>
        await _profile.GetProfileAsync(CurrentUserId, ct);

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromForm] UpdateProfileForm form, CancellationToken ct)
    {
        var request = new UpdateProfileRequest(
            form.FullName ?? string.Empty,
            form.Phone,
            string.IsNullOrWhiteSpace(form.Gender) ? null : form.Gender,
            form.DateOfBirth,
            form.Avatar is null ? null : new FileUpload(form.Avatar.FileName, form.Avatar.Length, form.Avatar.OpenReadStream),
            form.RemoveAvatar);
        return await _profile.UpdateProfileAsync(CurrentUserId, request, ct);
    }

    [HttpGet("addresses")]
    public async Task<ActionResult<IReadOnlyList<AddressDto>>> GetAddresses(CancellationToken ct) =>
        Ok(await _profile.GetAddressesAsync(CurrentUserId, ct));

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AddressRequest request, CancellationToken ct)
    {
        var address = await _profile.AddAddressAsync(CurrentUserId, request, ct);
        return Created($"/api/profile/addresses/{address.Id}", address);
    }

    [HttpPut("addresses/{id:int}")]
    public async Task<ActionResult<AddressDto>> UpdateAddress(int id, AddressRequest request, CancellationToken ct) =>
        await _profile.UpdateAddressAsync(CurrentUserId, id, request, ct);

    [HttpDelete("addresses/{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
    {
        await _profile.DeleteAddressAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpPut("addresses/{id:int}/default")]
    public async Task<IActionResult> SetDefaultAddress(int id, CancellationToken ct)
    {
        await _profile.SetDefaultAddressAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}

public sealed class UpdateProfileForm
{
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public IFormFile? Avatar { get; set; }

    public bool RemoveAvatar { get; set; }
}
