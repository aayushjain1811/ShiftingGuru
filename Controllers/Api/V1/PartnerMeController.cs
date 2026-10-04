using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): "who am I" for a signed-in partner. Also the simplest
/// test that a token works.
///
/// AuthenticationSchemes is the important part of [Authorize] here. Without
/// it, a request with no token would be redirected to the /partner/login web
/// page (the website's default) instead of getting a clean 401.
/// </summary>
[ApiController]
[Route("api/v1/partner")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AdminSeeder.VendorRole)]
public class PartnerMeController : ControllerBase
{
    private readonly IPartnerService _partners;

    public PartnerMeController(IPartnerService partners) => _partners = partners;

    // GET /api/v1/partner/me
    // Header: Authorization: Bearer <token>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        // The user id comes from the signed token, never from the request,
        // so one partner can never load another's details.
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiError("Please sign in again.", "invalidToken"));
        }

        var vendor = await _partners.GetByIdentityUserIdAsync(userId, ct: ct);

        if (vendor is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.MissingProfile);
        }

        // Checked against the database on every request, like the website's
        // PartnerControllerBase: a partner suspended after signing in is
        // locked out straight away, even with a token that hasn't expired.
        if (vendor.Status != VendorStatus.Approved)
        {
            return StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.For(vendor.Status));
        }

        return Ok(PartnerProfileDto.From(vendor));
    }
}