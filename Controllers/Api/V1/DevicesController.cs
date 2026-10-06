using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>What the app sends to turn push notifications on or off for this phone.</summary>
public class DeviceRequest
{
    [Required]
    [StringLength(200)]
    [RegularExpression(@"^Expo(nent)?PushToken\[[^\]]+\]$", ErrorMessage = "That isn't a valid push token.")]
    public string? Token { get; set; }

    [RegularExpression("^(android|ios)$")]
    public string? Platform { get; set; }
}

/// <summary>
/// NEW (mobile API): which phones receive push notifications.
///
/// The app registers its token after signing in, and removes it when logging
/// out. Works for partners and customers alike; the user comes from the token.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = AdminSeeder.VendorRole + "," + CustomerRoles.Customer)]
public class DevicesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DevicesController(ApplicationDbContext db) => _db = db;

    // POST /api/v1/devices   { "token": "ExponentPushToken[...]", "platform": "android" }
    [HttpPost("")]
    public async Task<IActionResult> Register([FromBody] DeviceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized(new ApiError("Please sign in again.", "invalidToken"));

        var now = DateTime.UtcNow;
        var token = request.Token!.Trim();

        // One phone belongs to whoever signed in on it last.
        var device = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token, ct);
        if (device is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                IdentityUserId = userId,
                Token = token,
                Platform = request.Platform ?? "android",
                CreatedAt = now,
                LastSeenAt = now
            });
        }
        else
        {
            device.IdentityUserId = userId;
            device.Platform = request.Platform ?? device.Platform;
            device.LastSeenAt = now;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two registrations of the same phone at once - the other one saved it.
        }

        return NoContent();
    }

    // DELETE /api/v1/devices   { "token": "ExponentPushToken[...]" }
    // Called when logging out, so this phone stops receiving this person's notifications.
    [HttpDelete("")]
    public async Task<IActionResult> Unregister([FromBody] DeviceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;
        var token = request.Token?.Trim();

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(token))
        {
            await _db.DeviceTokens
                .Where(d => d.Token == token && d.IdentityUserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        return NoContent();
    }
}