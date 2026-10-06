using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): the shapes of what the apps send and receive.
// These are kept separate from the entities on purpose: the API decides
// exactly which fields leave the server, and nothing else can slip out.

/// <summary>
/// Every error the API returns has this shape:
/// { "error": "message for the user", "code": "short word for the app" }
/// </summary>
public record ApiError(string Error, string? Code = null);

/// <summary>What the partner app sends to sign in.</summary>
public class PartnerLoginRequest
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter your password.")]
    public string? Password { get; set; }
}

/// <summary>
/// The partner details the app shows. No documents, no internal ids.
///
/// CHANGED: Services are now the catalog slugs (e.g. "home-shifting"), which
/// match the app's ServiceId values exactly. Rating, reviews, completed jobs,
/// service areas and join date were added for the app's profile and dashboard.
/// </summary>
public record PartnerProfileDto(
    string PartnerId,
    string BusinessName,
    string ContactPerson,
    string Email,
    string Phone,
    string City,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> ServiceAreas,
    double? Rating,
    int ReviewCount,
    int CompletedJobs,
    DateTime JoinedAt)
{
    public static PartnerProfileDto From(Vendor vendor, double? rating, int reviewCount, int completedJobs) => new(
        vendor.VendorNumber,
        vendor.BusinessName,
        vendor.ContactPerson,
        vendor.Email,
        vendor.Phone,
        vendor.City,
        vendor.Services.Select(s => s.ServiceSlug).ToList(),
        ServiceAreasOf(vendor),
        rating,
        reviewCount,
        completedJobs,
        vendor.CreatedAt);

    /// <summary>The home city first, then the operating locations, without repeats.</summary>
    private static IReadOnlyList<string> ServiceAreasOf(Vendor vendor)
    {
        var areas = new List<string> { vendor.City };

        if (!string.IsNullOrWhiteSpace(vendor.OperatingLocations))
        {
            areas.AddRange(vendor.OperatingLocations
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return areas
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// What a successful partner sign-in returns.
/// CHANGED: now includes a refresh token, so the app can stay signed in.
/// </summary>
public record PartnerLoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    PartnerProfileDto Partner);

/// <summary>NEW: what the app sends to get new tokens, or to sign out.</summary>
public class RefreshRequest
{
    [Required(ErrorMessage = "A refresh token is required.")]
    public string? RefreshToken { get; set; }
}

/// <summary>NEW: a fresh pair of tokens. The old refresh token no longer works.</summary>
public record TokenRefreshResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt);

/// <summary>NEW: a simple success message for the app to show.</summary>
public record ApiMessage(string Message);

/// <summary>NEW: step 1 of forgot password - "email me a code".</summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }
}

/// <summary>NEW: step 2 of forgot password - the code and the new password.</summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter the 6-digit code.")]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "The code is 6 digits.")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters, including a number.")]
    public string? NewPassword { get; set; }
}

/// <summary>Errors shared by several endpoints.</summary>
public static class ApiErrors
{
    public static readonly ApiError SessionExpired = new(
        "Your session has ended. Please sign in again.", "sessionExpired");
}

/// <summary>
/// The message and code for a partner who isn't approved. Used by sign-in and
/// by every signed-in endpoint, so the app always gets the same answer.
/// </summary>
public static class PartnerStatusErrors
{
    public static ApiError For(VendorStatus status) => status switch
    {
        VendorStatus.Pending => new(
            "Your application is under review. We'll email you once it's approved.", "pending"),

        VendorStatus.AwaitingPayment => new(
            "Please complete your registration payment on the ShiftingGuru website.", "awaitingPayment"),

        VendorStatus.Rejected => new(
            "Your application wasn't approved. Please contact support.", "rejected"),

        VendorStatus.Suspended => new(
            "Your account is not active. Please contact support.", "suspended"),

        _ => new("Your account can't sign in right now. Please contact support.", "unavailable")
    };

    public static readonly ApiError MissingProfile = new(
        "We couldn't find a partner profile for that account. Please contact support.", "missing");
}