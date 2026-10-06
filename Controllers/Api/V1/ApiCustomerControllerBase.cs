using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Auth;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): shared base for every signed-in customer endpoint - the
/// customer version of ApiPartnerControllerBase.
///
/// Before any action runs it loads the customer from the signed token (never
/// from the request) and checks the account is still active. Every query in
/// a customer controller then filters by CurrentCustomer.Id, so one customer
/// can never reach another's requests, quotes or bookings.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = CustomerRoles.Customer)]
public abstract class ApiCustomerControllerBase : ControllerBase, IAsyncActionFilter
{
    /// <summary>The signed-in, active customer. Set before every action.</summary>
    protected Customer CurrentCustomer { get; private set; } = null!;

    /// <summary>Controllers that change the customer row override this.</summary>
    protected virtual bool TracksCustomer => false;

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = User.FindFirst(ApiClaims.UserId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = Unauthorized(new ApiError("Please sign in again.", "invalidToken"));
            return;
        }

        var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var query = db.Customers.AsQueryable();
        if (!TracksCustomer) query = query.AsNoTracking();

        var customer = await query.FirstOrDefaultAsync(c => c.IdentityUserId == userId, HttpContext.RequestAborted);

        if (customer is null)
        {
            context.Result = StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("We couldn't find your account details. Please contact support.", "missing"));
            return;
        }

        if (!customer.IsActive)
        {
            context.Result = StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("Your account is not active. Please contact support.", "inactive"));
            return;
        }

        CurrentCustomer = customer;
        await next();
    }
}