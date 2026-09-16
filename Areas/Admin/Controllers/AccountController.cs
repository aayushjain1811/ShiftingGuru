using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin")]
public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly UserManager<IdentityUser> _users;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        ILogger<AccountController> logger)
    {
        _signIn = signIn;
        _users = users;
        _logger = logger;
    }

    // GET /admin/login
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(AdminSeeder.AdminRole))
        {
            return Redirect("/admin");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new AdminLoginViewModel());
    }

    // POST /admin/login
    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email);

        // One message for both "no such user" and "wrong password", so the
        // form can't be used to discover which email addresses exist.
        const string failed = "Those details don't match an admin account.";

        if (user is null || !await _users.IsInRoleAsync(user, AdminSeeder.AdminRole))
        {
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Admin sign-in blocked: account locked out.");
            ModelState.AddModelError(string.Empty,
                "Too many failed attempts. Try again in a few minutes.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            // Never log the email or the attempted password.
            _logger.LogWarning("Failed admin sign-in attempt.");
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        // Only allow local redirects - an open redirect here would be a
        // phishing vector straight off the login page.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/admin");
    }

    // POST /admin/logout - never a GET, so a stray link or prefetch can't sign you out.
    [HttpPost("logout")]
    [Authorize(Roles = AdminSeeder.AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Redirect("/admin/login");
    }

    // GET /admin/denied
    [HttpGet("denied")]
    [AllowAnonymous]
    public IActionResult Denied() => View();
}