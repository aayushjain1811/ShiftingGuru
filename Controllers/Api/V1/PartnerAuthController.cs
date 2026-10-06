using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Api;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): partner sign-in for the mobile app.
///
/// Same accounts, same passwords and same rules as /partner/login on the
/// website - only the result is different: a token instead of a cookie.
/// </summary>
[ApiController]
[Route("api/v1/auth/partner")]
[AllowAnonymous]
public class PartnerAuthController : ControllerBase
{
    // One message for "no such partner" and "wrong password", so this
    // endpoint can't be used to find out which emails belong to partners.
    private const string FailedMessage = "Those details don't match a partner account.";

    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly IPartnerService _partners;
    private readonly IPartnerProfileReader _profiles;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ILogger<PartnerAuthController> _logger;

    public PartnerAuthController(
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        IPartnerService partners,
        IPartnerProfileReader profiles,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        ILogger<PartnerAuthController> logger)
    {
        _users = users;
        _signIn = signIn;
        _partners = partners;
        _profiles = profiles;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    // POST /api/v1/auth/partner/login
    // Body: { "email": "...", "password": "..." }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PartnerLoginRequest request, CancellationToken ct)
    {
        // [ApiController] has already returned 400 if email or password is missing.

        var user = await _users.FindByEmailAsync(request.Email!.Trim());

        if (user is null || !await _users.IsInRoleAsync(user, AdminSeeder.VendorRole))
        {
            return Unauthorized(new ApiError(FailedMessage, "invalidCredentials"));
        }

        // CheckPasswordSignInAsync, NOT PasswordSignInAsync: it checks the
        // password and counts failures for lockout, but doesn't create a
        // website cookie, which an app has no use for.
        var result = await _signIn.CheckPasswordSignInAsync(
            user, request.Password!, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiError("Too many failed attempts. Try again in a few minutes.", "lockedOut"));
        }

        if (!result.Succeeded)
        {
            // Never log the email or the attempted password.
            _logger.LogWarning("Failed partner sign-in attempt from the mobile app.");
            return Unauthorized(new ApiError(FailedMessage, "invalidCredentials"));
        }

        // The password was right. Whether they may get in is a separate question.
        var vendor = await _partners.GetByIdentityUserIdAsync(user.Id, ct: ct);

        if (vendor is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.MissingProfile);
        }

        if (vendor.Status != VendorStatus.Approved)
        {
            return StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.For(vendor.Status));
        }

        var token = _tokens.CreateAccessToken(user, AdminSeeder.VendorRole);

        // NEW: the long-lived token that keeps this phone signed in.
        var refresh = await _refreshTokens.IssueAsync(user);

        var profile = await _profiles.ReadAsync(vendor, ct);

        return Ok(new PartnerLoginResponse(
            token.AccessToken, token.ExpiresAt, refresh.Token, refresh.ExpiresAt, profile));
    }
}