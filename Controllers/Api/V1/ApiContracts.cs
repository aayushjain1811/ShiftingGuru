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

/// <summary>The partner details the app shows. No documents, no internal ids.</summary>
public record PartnerProfileDto(
    string PartnerId,
    string BusinessName,
    string ContactPerson,
    string Email,
    string Phone,
    string City,
    IReadOnlyList<string> Services)
{
    public static PartnerProfileDto From(Vendor vendor) => new(
        vendor.VendorNumber,
        vendor.BusinessName,
        vendor.ContactPerson,
        vendor.Email,
        vendor.Phone,
        vendor.City,
        vendor.Services.Select(s => s.ServiceName).ToList());
}

/// <summary>What a successful partner sign-in returns.</summary>
public record PartnerLoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    PartnerProfileDto Partner);

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