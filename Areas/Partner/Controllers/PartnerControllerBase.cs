using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;

namespace ShiftingGuru.Areas.Partner.Controllers;

/// <summary>
/// Shared base for every signed-in partner page.
///
/// Approval status is checked against the database on each request, not read
/// from the auth cookie: a cookie issued before an admin suspended the account
/// would still claim the vendor is approved. The cookie proves who you are;
/// the database decides what you may do.
/// </summary>
[Area("Partner")]
[Authorize(Roles = AdminSeeder.VendorRole)]
public abstract class PartnerControllerBase : Controller
{
    protected readonly IPartnerService Partners;
    private readonly UserManager<IdentityUser> _users;

    protected Vendor CurrentVendor { get; private set; } = null!;

    protected PartnerControllerBase(IPartnerService partners, UserManager<IdentityUser> users)
    {
        Partners = partners;
        _users = users;
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Identity id comes from the authentication context, never from a
        // route value or form field, so one vendor can't load another's data.
        var userId = _users.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = Redirect("/partner/login");
            return;
        }

        var vendor = await Partners.GetByIdentityUserIdAsync(userId, tracked: TracksVendor);

        if (vendor is null)
        {
            // In the Vendor role but no vendor record - shouldn't happen, but
            // signing them out is safer than showing a half-broken dashboard.
            context.Result = Redirect("/partner/login?problem=missing");
            return;
        }

        if (vendor.Status != VendorStatus.Approved)
        {
            context.Result = RedirectToAction("Pending", "Account", new { area = "Partner" });
            return;
        }

        CurrentVendor = vendor;
        ViewData["VendorBusinessName"] = vendor.BusinessName;

        await next();
    }

    /// <summary>Controllers that write override this to get a tracked entity.</summary>
    protected virtual bool TracksVendor => false;
}