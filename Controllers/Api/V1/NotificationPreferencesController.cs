using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>The four notification switches. Partners and customers use the same shape.</summary>
public record NotificationPreferencesDto(bool Leads, bool Quotes, bool Bookings, bool Reviews);

/// <summary>
/// NEW (mobile API): the signed-in person's push notification switches.
/// Saved on the server, so they apply to every phone the person uses.
/// </summary>
[ApiController]
[Route("api/v1/notifications/preferences")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = AdminSeeder.VendorRole + "," + CustomerRoles.Customer)]
public class NotificationPreferencesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotificationPreferencesController(ApplicationDbContext db) => _db = db;

    // GET /api/v1/notifications/preferences
    [HttpGet("")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized(new ApiError("Please sign in again.", "invalidToken"));

        var row = await _db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId, ct);

        // No row yet: everything is on.
        return Ok(row is null
            ? new NotificationPreferencesDto(true, true, true, true)
            : new NotificationPreferencesDto(row.Leads, row.Quotes, row.Bookings, row.Reviews));
    }

    // PUT /api/v1/notifications/preferences   { "leads": true, "quotes": false, ... }
    [HttpPut("")]
    public async Task<IActionResult> Update([FromBody] NotificationPreferencesDto request, CancellationToken ct)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized(new ApiError("Please sign in again.", "invalidToken"));

        var row = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.IdentityUserId == userId, ct);
        if (row is null)
        {
            row = new NotificationPreference { IdentityUserId = userId };
            _db.NotificationPreferences.Add(row);
        }

        row.Leads = request.Leads;
        row.Quotes = request.Quotes;
        row.Bookings = request.Bookings;
        row.Reviews = request.Reviews;
        row.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two saves at the same moment from two phones - the other one created the row.
            return Conflict(new ApiError("Please try again.", "retry"));
        }

        return Ok(new NotificationPreferencesDto(row.Leads, row.Quotes, row.Bookings, row.Reviews));
    }
}