using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the signed-in customer's own details - "who am I" when
/// the app opens, and editing name, email and city.
///
/// The mobile number can't be changed here: it's the sign-in, it was proven
/// by SMS, and partners use it to reach the customer.
/// </summary>
[Route("api/v1/customer")]
public class CustomerMeController : ApiCustomerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly ILogger<CustomerMeController> _logger;

    public CustomerMeController(
        ApplicationDbContext db, UserManager<IdentityUser> users, ILogger<CustomerMeController> logger)
    {
        _db = db;
        _users = users;
        _logger = logger;
    }

    // This controller writes, so it needs the tracked customer row.
    protected override bool TracksCustomer => true;

    // GET /api/v1/customer/me
    [HttpGet("me")]
    public IActionResult Me() => Ok(CustomerProfileDto.From(CurrentCustomer));

    // PUT /api/v1/customer/me
    [HttpPut("me")]
    public async Task<IActionResult> Update([FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = CurrentCustomer;
        var email = request.Email!.Trim();

        var user = await _users.FindByIdAsync(customer.IdentityUserId);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("We couldn't find your account details. Please contact support.", "missing"));
        }

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            // Emails are unique across every account - customers, partners and admins.
            var owner = await _users.FindByEmailAsync(email);
            if (owner is not null && owner.Id != user.Id)
            {
                return Conflict(new ApiError(
                    "This email is already used by another ShiftingGuru account. Use a different email.", "emailTaken"));
            }

            // Set directly rather than with SetEmailAsync: that one also changes
            // the security stamp, which would sign the customer out of the app.
            user.Email = email;
            user.NormalizedEmail = _users.NormalizeEmail(email);
            user.EmailConfirmed = false;

            var updated = await _users.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                return BadRequest(new ApiError(
                    string.Join(" ", updated.Errors.Select(e => e.Description)), "badEmail"));
            }
        }

        customer.FullName = request.FullName!.Trim();
        customer.Email = email;
        customer.City = request.City!.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile update from the app failed for customer {CustomerId}", customer.Id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Couldn't save those changes. Please try again.", "saveFailed"));
        }

        return Ok(CustomerProfileDto.From(customer));
    }
}