using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): keeping a session alive, and ending it.
///
/// The access token lasts minutes; the refresh token lasts weeks. When the
/// access token runs out, the app sends its refresh token here and gets a new
/// pair back, without the person noticing anything.
///
/// [AllowAnonymous] because the refresh token itself is the proof - the
/// access token has usually already expired when this is called.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthSessionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ITokenService _tokens;
    private readonly IPartnerService _partners;

    public AuthSessionController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IRefreshTokenService refreshTokens,
        ITokenService tokens,
        IPartnerService partners)
    {
        _db = db;
        _users = users;
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _partners = partners;
    }

    // POST /api/v1/auth/refresh
    // Body: { "refreshToken": "..." }
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var match = await _refreshTokens.FindAsync(request.RefreshToken);
        if (match is null) return Unauthorized(ApiErrors.SessionExpired);

        var user = match.User;

        // ---- Partners ----
        if (await _users.IsInRoleAsync(user, AdminSeeder.VendorRole))
        {
            // The same approval check as sign-in. A partner suspended since they
            // signed in is stopped here, and their session on this phone ends.
            var vendor = await _partners.GetByIdentityUserIdAsync(user.Id, ct: ct);

            if (vendor is null || vendor.Status != VendorStatus.Approved)
            {
                await _refreshTokens.RevokeAsync(request.RefreshToken);
                return StatusCode(StatusCodes.Status403Forbidden,
                    vendor is null ? PartnerStatusErrors.MissingProfile : PartnerStatusErrors.For(vendor.Status));
            }

            return Ok(await RotateAsync(match, AdminSeeder.VendorRole));
        }

        // ---- Customers (NEW) ----
        if (await _users.IsInRoleAsync(user, CustomerRoles.Customer))
        {
            var customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdentityUserId == user.Id, ct);

            if (customer is null || !customer.IsActive)
            {
                await _refreshTokens.RevokeAsync(request.RefreshToken);
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiError("Your account is not active. Please contact support.", "inactive"));
            }

            return Ok(await RotateAsync(match, CustomerRoles.Customer));
        }

        // Anyone else (for example an admin) doesn't use the apps.
        await _refreshTokens.RevokeAsync(request.RefreshToken);
        return Unauthorized(ApiErrors.SessionExpired);
    }

    /// <summary>Spends the old refresh token and hands out a new pair.</summary>
    private async Task<TokenRefreshResponse> RotateAsync(RefreshTokenMatch match, string role)
    {
        var refresh = await _refreshTokens.RotateAsync(match);
        var access = _tokens.CreateAccessToken(match.User, role);
        return new TokenRefreshResponse(access.AccessToken, access.ExpiresAt, refresh.Token, refresh.ExpiresAt);
    }

    // POST /api/v1/auth/logout
    // Body: { "refreshToken": "..." }
    // Always 204, whether or not the token was still valid: the result for
    // the app is the same either way - this phone is signed out.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _refreshTokens.RevokeAsync(request.RefreshToken);
        return NoContent();
    }
}