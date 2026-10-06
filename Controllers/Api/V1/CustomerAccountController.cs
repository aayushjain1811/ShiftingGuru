using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>What the app sends to delete the account: the password, to prove it's really them.</summary>
public class DeleteAccountRequest
{
    [Required(ErrorMessage = "Enter your password to confirm.")]
    public string? Password { get; set; }
}

/// <summary>
/// NEW (mobile API): a customer deletes their own account - required by
/// Google Play for any app where people can create an account.
///
/// Deletes everything, permanently, in one transaction:
///   - the customer's requests (made in the app, and website requests made
///     with their verified mobile number), with every quote, partner
///     assignment and access link on them
///   - their reviews
///   - the email log entries about those requests
///   - their phones' push tokens and notification switches
///   - the Customer row and the sign-in itself (which also ends every session)
///
/// One safety rule: not while a booked move is still in progress, so a
/// partner isn't left with a job and no customer record.
/// </summary>
[Route("api/v1/customer/account")]
public class CustomerAccountController : ApiCustomerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly ILogger<CustomerAccountController> _logger;

    public CustomerAccountController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        ILogger<CustomerAccountController> logger)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _logger = logger;
    }

    // POST /api/v1/customer/account/delete   { "password": "..." }
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        var customer = CurrentCustomer;

        var user = await _users.FindByIdAsync(customer.IdentityUserId);
        if (user is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("We couldn't find your account details. Please contact support.", "missing"));
        }

        // The password proves it's really the account owner, not someone holding an unlocked phone.
        var check = await _signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiError("Too many attempts. Try again in a few minutes.", "lockedOut"));
        }
        if (!check.Succeeded)
        {
            return BadRequest(new ApiError("That password isn't right.", "wrongPassword"));
        }

        var phone = customer.Phone;

        // Their requests: linked to the account, plus any website request made
        // with their verified number that hasn't been linked yet.
        var leadIds = await _db.Leads
            .Where(l => l.CustomerId == customer.Id
                     || (l.CustomerId == null && (l.Phone == phone || l.Phone.EndsWith(phone))))
            .Select(l => l.Id)
            .ToListAsync(ct);

        if (await _db.Leads.AnyAsync(l => leadIds.Contains(l.Id) && l.Status == LeadStatus.Converted, ct))
        {
            return Conflict(new ApiError(
                "You have a booked move that isn't complete yet. Once it's done - or once you've cancelled it with your partner and told us - you can delete your account.",
                "activeBooking"));
        }

        var quoteIds = await _db.Quotes.Where(q => leadIds.Contains(q.LeadId)).Select(q => q.Id).ToListAsync(ct);
        var reviewIds = await _db.Reviews.Where(r => leadIds.Contains(r.LeadId)).Select(r => r.Id).ToListAsync(ct);
        var email = customer.Email.ToLower();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // 1. Unhook the chosen quote first: a request points at its chosen
            //    quote, and that quote is about to be deleted with the request.
            await _db.Leads
                .Where(l => leadIds.Contains(l.Id))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(l => l.SelectedQuoteId, (int?)null)
                    .SetProperty(l => l.SelectedVendorId, (int?)null), ct);

            // 2. Reviews of those moves.
            await _db.Reviews.Where(r => leadIds.Contains(r.LeadId)).ExecuteDeleteAsync(ct);

            // 3. Email log entries about them, and anything emailed to this customer.
            await _db.NotificationLogs
                .Where(n => n.Recipient.ToLower() == email
                         || (n.EntityType == nameof(Lead) && leadIds.Contains(n.EntityId))
                         || (n.EntityType == nameof(Quote) && quoteIds.Contains(n.EntityId))
                         || (n.EntityType == nameof(Review) && reviewIds.Contains(n.EntityId)))
                .ExecuteDeleteAsync(ct);

            // 4. The requests themselves. The database removes their quotes,
            //    partner assignments and access links with them.
            await _db.Leads.Where(l => leadIds.Contains(l.Id)).ExecuteDeleteAsync(ct);

            // 5. Phones and notification switches.
            await _db.DeviceTokens.Where(d => d.IdentityUserId == user.Id).ExecuteDeleteAsync(ct);
            await _db.NotificationPreferences.Where(p => p.IdentityUserId == user.Id).ExecuteDeleteAsync(ct);

            // 6. The customer, then the sign-in (which takes the refresh tokens with it).
            await _db.Customers.Where(c => c.Id == customer.Id).ExecuteDeleteAsync(ct);

            var deleted = await _users.DeleteAsync(user);
            if (!deleted.Succeeded)
            {
                throw new InvalidOperationException("Couldn't delete the sign-in: " +
                    string.Join("; ", deleted.Errors.Select(e => e.Description)));
            }

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(ex, "Account deletion failed for customer {CustomerId}; nothing was deleted.", customer.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError(
                "Sorry, we couldn't delete your account just now. Nothing was removed - please try again, or contact support.",
                "deleteFailed"));
        }

        // No personal details in the log - just that it happened.
        _logger.LogInformation("A customer deleted their account ({Requests} request(s) removed).", leadIds.Count);

        return NoContent();
    }
}