using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Auth;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Areas.Partner.Controllers;

[Area("Partner")]
[Route("partner")]
public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly UserManager<IdentityUser> _users;
    private readonly IPartnerService _partners;
    private readonly IPartnerPasswordResetService _reset;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        IPartnerService partners,
        IPartnerPasswordResetService reset,
        ILogger<AccountController> logger)
    {
        _signIn = signIn;
        _users = users;
        _partners = partners;
        _reset = reset;
        _logger = logger;
    }

    // GET /partner/login
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? problem = null)
    {
        if (problem == "missing")
        {
            ModelState.AddModelError(string.Empty,
                "We couldn't find a partner profile for that account. Please contact support.");
        }

        ViewData["Title"] = "Partner Login";
        return View(new VendorLoginViewModel());
    }

    // POST /partner/login
    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(VendorLoginViewModel model)
    {
        ViewData["Title"] = "Partner Login";
        if (!ModelState.IsValid) return View(model);

        const string failed = "Those details don't match a partner account.";

        var user = await _users.FindByEmailAsync(model.Email);

        if (user is null || !await _users.IsInRoleAsync(user, AdminSeeder.VendorRole))
        {
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Too many failed attempts. Try again in a few minutes.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed partner sign-in attempt.");
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        // Signed in successfully. Approval is a separate question.
        var vendor = await _partners.GetByIdentityUserIdAsync(user.Id);

        if (vendor is null)
        {
            await _signIn.SignOutAsync();
            ModelState.AddModelError(string.Empty,
                "We couldn't find a partner profile for that account. Please contact support.");
            return View(model);
        }

        if (vendor.Status != VendorStatus.Approved)
        {
            return RedirectToAction(nameof(Pending));
        }

        return Redirect("/partner/dashboard");
    }

    // GET /partner/status - explains why an account can't get in yet.
    [HttpGet("status")]
    [Authorize(Roles = AdminSeeder.VendorRole)]
    public async Task<IActionResult> Pending()
    {
        var vendor = await _partners.GetByIdentityUserIdAsync(_users.GetUserId(User) ?? "");
        if (vendor is null) return Redirect("/partner/login?problem=missing");

        if (vendor.Status == VendorStatus.Approved) return Redirect("/partner/dashboard");

        ViewData["Title"] = "Application status";
        return View(vendor);
    }

    // POST /partner/logout
    [HttpPost("logout")]
    [Authorize(Roles = AdminSeeder.VendorRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Redirect("/partner/login");
    }

    // -----------------------------------------------------------------
    // NEW: forgot password. Same rules as the mobile app, from the same
    // service - a code emailed here works in the app and the other way round.
    // -----------------------------------------------------------------

    // GET /partner/forgot-password
    [HttpGet("forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot password";
        ViewData["Robots"] = "noindex, nofollow";
        return View(new PartnerForgotPasswordViewModel());
    }

    // POST /partner/forgot-password
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(PartnerForgotPasswordViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Forgot password";
        ViewData["Robots"] = "noindex, nofollow";

        if (!ModelState.IsValid) return View(model);

        await _reset.SendCodeAsync(model.Email!, ct);

        // Carried to the next page in TempData, not the URL, so the email
        // address never lands in browser history or server logs.
        TempData["ResetEmail"] = model.Email!.Trim();

        return RedirectToAction(nameof(ResetPassword));
    }

    // GET /partner/reset-password
    [HttpGet("reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword()
    {
        ViewData["Title"] = "Set a new password";
        ViewData["Robots"] = "noindex, nofollow";

        // Same message whether or not the email has an account.
        ViewData["CodeSentMessage"] =
            "If that email belongs to a partner account, we've sent a 6-digit code to it. It works for 5 minutes.";

        return View(new PartnerResetPasswordViewModel { Email = TempData["ResetEmail"] as string });
    }

    // POST /partner/reset-password
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(PartnerResetPasswordViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Set a new password";
        ViewData["Robots"] = "noindex, nofollow";

        if (!ModelState.IsValid) return View(model);

        var result = await _reset.ResetAsync(model.Email!, model.Code!, model.NewPassword!, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);

            // Passwords are never sent back to the page.
            model.NewPassword = null;
            model.ConfirmPassword = null;
            return View(model);
        }

        TempData["PartnerLoginMessage"] = "Your password has been changed. Sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }
}