using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): "forgot password" for partners, in two steps.
///
///   1. forgot-password: email me a 6-digit code
///   2. reset-password:  here's the code and my new password
///
/// CHANGED: all the rules now live in PartnerPasswordResetService, which the
/// website's forgot-password pages use too.
/// </summary>
[ApiController]
[Route("api/v1/auth/partner")]
[AllowAnonymous]
public class PartnerPasswordResetController : ControllerBase
{
    // The same answer whether or not the email is a partner, so this endpoint
    // can't be used to find out which emails have accounts.
    private const string SentMessage =
        "If that email belongs to a partner account, we've sent a 6-digit code to it.";

    private readonly IPartnerPasswordResetService _reset;

    public PartnerPasswordResetController(IPartnerPasswordResetService reset) => _reset = reset;

    // POST /api/v1/auth/partner/forgot-password
    // Body: { "email": "..." }
    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _reset.SendCodeAsync(request.Email!, ct);
        return Ok(new ApiMessage(SentMessage));
    }

    // POST /api/v1/auth/partner/reset-password
    // Body: { "email": "...", "code": "123456", "newPassword": "..." }
    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _reset.ResetAsync(request.Email!, request.Code!, request.NewPassword!, ct);

        if (result.Succeeded)
        {
            return Ok(new ApiMessage("Your password has been changed. Sign in with your new password."));
        }

        var error = new ApiError(result.Error!, result.ErrorCode);

        return result.ErrorCode == "lockedOut"
            ? StatusCode(StatusCodes.Status429TooManyRequests, error)
            : BadRequest(error);
    }
}