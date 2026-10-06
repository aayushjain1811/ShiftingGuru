using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Auth;
using ShiftingGuru.Services.Verification;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): customer registration and sign-in for the app.
///
/// Registration, in the app:
///   1. check    - is this mobile number and email free? (before spending an SMS)
///   2. the app sends the SMS code with Firebase and the customer types it in
///   3. register - the app sends the details plus Firebase's proof; the server
///                 asks Firebase directly whether this exact number was verified
///
/// Uses the same Identity system as partners and admins, with a new
/// "Customer" role. The website's own customers are unaffected: they still
/// use the quote form and the magic link without an account.
/// </summary>
[ApiController]
[Route("api/v1/auth/customer")]
[AllowAnonymous]
public class CustomerAuthController : ControllerBase
{
    private const string FailedMessage = "Those details don't match an account.";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IPhoneVerificationService _phoneVerification;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ILogger<CustomerAuthController> _logger;

    public CustomerAuthController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        RoleManager<IdentityRole> roles,
        IPhoneVerificationService phoneVerification,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        ILogger<CustomerAuthController> logger)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _roles = roles;
        _phoneVerification = phoneVerification;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    // POST /api/v1/auth/customer/check
    // Body: { "phone": "9876543210", "email": "..." }
    // Called before the SMS is sent, so nobody pays for a code that can't be used.
    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] CustomerCheckRequest request)
    {
        var phone = LeadService.MobileDigits(request.Phone);
        if (phone is null)
        {
            return BadRequest(new ApiError("Enter a valid 10-digit Indian mobile number.", "badPhone"));
        }

        return await TakenAsync(phone, request.Email!.Trim()) ?? Ok(new ApiMessage("Looks good."));
    }

    // POST /api/v1/auth/customer/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CustomerRegisterRequest request, CancellationToken ct)
    {
        var phone = LeadService.MobileDigits(request.Phone);
        if (phone is null)
        {
            return BadRequest(new ApiError("Enter a valid 10-digit Indian mobile number.", "badPhone"));
        }

        if (!request.AcceptTerms)
        {
            return BadRequest(new ApiError("Please accept the Terms & Conditions and Privacy Policy.", "termsRequired"));
        }

        var email = request.Email!.Trim();

        if (await TakenAsync(phone, email) is { } taken) return taken;

        // The proof must be for THIS number - asked of Firebase, not taken from the app.
        if (!await _phoneVerification.IsVerifiedAsync(request.FirebaseToken, phone, ct))
        {
            return BadRequest(new ApiError(
                "We couldn't confirm your mobile number. Please request a new code and try again.", "phoneNotVerified"));
        }

        await EnsureCustomerRoleAsync();

        var now = DateTime.UtcNow;
        IdentityUser user;
        Customer customer;
        int linked;

        // Everything that writes, in one transaction: all of it, or none of it.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            user = new IdentityUser
            {
                UserName = phone,           // customers sign in with their mobile number
                Email = email,
                PhoneNumber = phone,
                PhoneNumberConfirmed = true,
                EmailConfirmed = false
            };

            var created = await _users.CreateAsync(user, request.Password!);
            if (!created.Succeeded)
            {
                await transaction.RollbackAsync(ct);

                // Identity's messages cover the password rules. They never repeat the password.
                return BadRequest(new ApiError(
                    string.Join(" ", created.Errors.Select(e => e.Description)), "weakPassword"));
            }

            var role = await _users.AddToRoleAsync(user, CustomerRoles.Customer);
            if (!role.Succeeded)
            {
                throw new InvalidOperationException("Couldn't add the Customer role: " +
                    string.Join("; ", role.Errors.Select(e => e.Description)));
            }

            customer = new Customer
            {
                IdentityUserId = user.Id,
                FullName = request.FullName!.Trim(),
                Phone = phone,
                Email = email,
                City = request.City!.Trim(),
                PhoneVerifiedAt = now,
                TermsAcceptedAt = now,
                IsActive = true,
                CreatedAt = now
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);

            // Earlier website requests from this (now verified) number become
            // this customer's. EndsWith also catches older leads saved as "+91 ...".
            linked = await _db.Leads
                .Where(l => l.CustomerId == null && (l.Phone == phone || l.Phone.EndsWith(phone)))
                .ExecuteUpdateAsync(set => set.SetProperty(l => l.CustomerId, customer.Id), ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Customer registration failed; rolling everything back.");
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackError) { _logger.LogError(rollbackError, "Rollback also failed."); }

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError(
                "Sorry, we couldn't create your account just now. Please try again in a moment.", "registerFailed"));
        }

        _logger.LogInformation("A customer registered in the app and {Linked} earlier request(s) were linked.", linked);

        return Ok(await SignedInAsync(user, customer, linked));
    }

    // POST /api/v1/auth/customer/login
    // Body: { "phone": "9876543210", "password": "..." }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] CustomerLoginRequest request, CancellationToken ct)
    {
        var phone = LeadService.MobileDigits(request.Phone);
        var user = phone is null ? null : await _users.FindByNameAsync(phone);

        // One message for "no account" and "wrong password", so this can't be
        // used to find out which numbers have accounts.
        if (user is null || !await _users.IsInRoleAsync(user, CustomerRoles.Customer))
        {
            return Unauthorized(new ApiError(FailedMessage, "invalidCredentials"));
        }

        var result = await _signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiError("Too many failed attempts. Try again in a few minutes.", "lockedOut"));
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed customer sign-in attempt from the mobile app.");
            return Unauthorized(new ApiError(FailedMessage, "invalidCredentials"));
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityUserId == user.Id, ct);

        if (customer is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("We couldn't find your account details. Please contact support.", "missing"));
        }

        if (!customer.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("Your account is not active. Please contact support.", "inactive"));
        }

        return Ok(await SignedInAsync(user, customer, 0));
    }

    // POST /api/v1/auth/customer/forgot-password/check
    // Body: { "phone": "9876543210" }
    // Called before the SMS is sent, so no code is sent to a number without an account.
    [HttpPost("forgot-password/check")]
    public async Task<IActionResult> ForgotCheck([FromBody] CustomerResetCheckRequest request)
    {
        var phone = LeadService.MobileDigits(request.Phone);
        if (phone is null)
        {
            return BadRequest(new ApiError("Enter a valid 10-digit Indian mobile number.", "badPhone"));
        }

        var user = await _users.FindByNameAsync(phone);
        if (user is null || !await _users.IsInRoleAsync(user, CustomerRoles.Customer))
        {
            return NotFound(new ApiError(
                "There's no account with this number. Create one instead.", "noAccount"));
        }

        return Ok(new ApiMessage("Looks good."));
    }

    // POST /api/v1/auth/customer/reset-password
    // Body: { "phone": "...", "firebaseToken": "...", "newPassword": "..." }
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] CustomerResetPasswordRequest request, CancellationToken ct)
    {
        var phone = LeadService.MobileDigits(request.Phone);
        if (phone is null)
        {
            return BadRequest(new ApiError("Enter a valid 10-digit Indian mobile number.", "badPhone"));
        }

        // The SMS code proves the person holds this phone - asked of Firebase, not taken from the app.
        if (!await _phoneVerification.IsVerifiedAsync(request.FirebaseToken, phone, ct))
        {
            return BadRequest(new ApiError(
                "We couldn't confirm your mobile number. Please request a new code and try again.", "phoneNotVerified"));
        }

        var user = await _users.FindByNameAsync(phone);
        if (user is null || !await _users.IsInRoleAsync(user, CustomerRoles.Customer))
        {
            return NotFound(new ApiError("There's no account with this number.", "noAccount"));
        }

        var resetToken = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, resetToken, request.NewPassword!);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiError(
                string.Join(" ", result.Errors.Select(e => e.Description)), "weakPassword"));
        }

        // Proving the phone also clears any lockout from wrong passwords.
        await _users.ResetAccessFailedCountAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);

        // The password change also ends every phone's session (refresh tokens
        // check the security stamp), so a lost phone is signed out too.
        _logger.LogInformation("A customer reset their password from the mobile app.");

        return Ok(new ApiMessage("Your password has been changed. Log in with your new password."));
    }

    // -----------------------------------------------------------------

    /// <summary>A 409 if the number or email already has an account; null when both are free.</summary>
    private async Task<IActionResult?> TakenAsync(string phone, string email)
    {
        if (await _users.FindByNameAsync(phone) is not null ||
            await _db.Customers.AsNoTracking().AnyAsync(c => c.Phone == phone))
        {
            return Conflict(new ApiError(
                "An account already exists with this mobile number. Log in instead.", "phoneTaken"));
        }

        // Emails are unique across every account - customers, partners and admins.
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return Conflict(new ApiError(
                "This email is already used by another ShiftingGuru account. Use a different email.", "emailTaken"));
        }

        return null;
    }

    private async Task EnsureCustomerRoleAsync()
    {
        if (await _roles.RoleExistsAsync(CustomerRoles.Customer)) return;

        var result = await _roles.CreateAsync(new IdentityRole(CustomerRoles.Customer));

        // Two first registrations at the same moment: the other one created it. Fine.
        if (!result.Succeeded && !await _roles.RoleExistsAsync(CustomerRoles.Customer))
        {
            throw new InvalidOperationException("Couldn't create the Customer role.");
        }
    }

    private async Task<CustomerAuthResponse> SignedInAsync(IdentityUser user, Customer customer, int linked)
    {
        var access = _tokens.CreateAccessToken(user, CustomerRoles.Customer);
        var refresh = await _refreshTokens.IssueAsync(user);

        return new CustomerAuthResponse(
            access.AccessToken, access.ExpiresAt,
            refresh.Token, refresh.ExpiresAt,
            CustomerProfileDto.From(customer),
            linked);
    }
}