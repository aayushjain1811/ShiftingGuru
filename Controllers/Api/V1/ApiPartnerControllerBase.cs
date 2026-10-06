using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): shared base for every signed-in partner endpoint.
/// The API's version of the website's PartnerControllerBase.
///
/// Before any action runs, it:
///   1. reads the user id from the signed token (never from the request)
///   2. loads that partner from the database
///   3. checks they are STILL approved - a partner suspended after signing
///      in is stopped here, even with a token that hasn't expired
///
/// Then CurrentVendor is ready for the action, and every query in the
/// controller filters by CurrentVendor.Id - so one partner can never reach
/// another partner's leads, quotes or bookings.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AdminSeeder.VendorRole)]
public abstract class ApiPartnerControllerBase : ControllerBase, IAsyncActionFilter
{
    /// <summary>The signed-in, approved partner. Set before every action.</summary>
    protected Vendor CurrentVendor { get; private set; } = null!;

    /// <summary>Controllers that change the vendor row override this to get a tracked entity.</summary>
    protected virtual bool TracksVendor => false;

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = Unauthorized(new ApiError("Please sign in again.", "invalidToken"));
            return;
        }

        var partners = HttpContext.RequestServices.GetRequiredService<IPartnerService>();
        var vendor = await partners.GetByIdentityUserIdAsync(userId, TracksVendor, HttpContext.RequestAborted);

        if (vendor is null)
        {
            context.Result = StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.MissingProfile);
            return;
        }

        if (vendor.Status != VendorStatus.Approved)
        {
            context.Result = StatusCode(StatusCodes.Status403Forbidden, PartnerStatusErrors.For(vendor.Status));
            return;
        }

        CurrentVendor = vendor;
        await next();
    }
}