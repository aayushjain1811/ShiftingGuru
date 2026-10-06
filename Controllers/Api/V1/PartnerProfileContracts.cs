using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): the partner's own business profile - the same fields the
// website's /partner/profile page edits.

/// <summary>Everything the "Business information" screen shows.</summary>
public record PartnerBusinessDto(
    string PartnerId,
    string BusinessName,
    string ContactPerson,
    string Email,
    string Phone,
    string City,
    string Address,
    string? GstNumber,
    int YearsOfExperience,
    string? AdditionalInformation,
    int ProfileCompletion,
    IReadOnlyList<string> MissingProfileFields)
{
    public static PartnerBusinessDto From(Vendor vendor)
    {
        var (completion, missing) = PartnerProfileCompletion.For(vendor);

        return new PartnerBusinessDto(
            vendor.VendorNumber,
            vendor.BusinessName,
            vendor.ContactPerson,
            vendor.Email,
            vendor.Phone,
            vendor.City,
            vendor.Address,
            vendor.GstNumber,
            vendor.YearsOfExperience,
            vendor.AdditionalInformation,
            completion,
            missing);
    }
}

/// <summary>
/// What the app sends to update business details.
///
/// Email and mobile number are NOT here on purpose: the email is the sign-in
/// for the website and the app, and changing either needs ShiftingGuru
/// support, so nobody can take over an account from a stolen phone.
/// </summary>
public class UpdateBusinessRequest
{
    [Required(ErrorMessage = "Enter your business name.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Business name should be 3 to 200 characters.")]
    public string? BusinessName { get; set; }

    [Required(ErrorMessage = "Enter the contact person's name.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Contact name should be 2 to 120 characters.")]
    public string? ContactPerson { get; set; }

    [Required(ErrorMessage = "Enter your city.")]
    [StringLength(80, ErrorMessage = "City should be under 80 characters.")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Enter your business address.")]
    [StringLength(400, MinimumLength = 5, ErrorMessage = "Address should be 5 to 400 characters.")]
    public string? Address { get; set; }

    // The standard 15-character GSTIN, e.g. 06ABCDE1234F1Z5. Optional.
    [RegularExpression(@"^\s*[0-9]{2}[A-Za-z]{5}[0-9]{4}[A-Za-z][1-9A-Za-z][Zz][0-9A-Za-z]\s*$",
        ErrorMessage = "Enter a valid 15-character GST number, or leave it empty.")]
    public string? GstNumber { get; set; }

    [Range(0, 100, ErrorMessage = "Years of experience should be between 0 and 100.")]
    public int YearsOfExperience { get; set; }

    [StringLength(1000, ErrorMessage = "Keep the description under 1000 characters.")]
    public string? AdditionalInformation { get; set; }
}

/// <summary>What the app sends to change services and covered cities.</summary>
public class UpdateServicesAreasRequest
{
    /// <summary>Catalog slugs, e.g. "home-shifting". At least one.</summary>
    [Required]
    public List<string> Services { get; set; } = new();

    /// <summary>Cities covered besides the home city. The home city is always included.</summary>
    public List<string> Areas { get; set; } = new();
}

/// <summary>After a save: the business details, and the profile the rest of the app uses.</summary>
public record UpdatedBusinessResponse(PartnerBusinessDto Business, PartnerProfileDto Partner);

/// <summary>One partner document, without the file. Documents are viewed only by the admin team.</summary>
public record PartnerDocumentDto(
    string Type,
    string Label,
    bool Uploaded,
    string? FileName,
    DateTime? UploadedAt);

/// <summary>Same rule as the website's partner dashboard.</summary>
public static class PartnerProfileCompletion
{
    private const int RequiredFields = 6;   // always filled at registration
    private const int TotalFields = 10;

    public static (int Percent, IReadOnlyList<string> Missing) For(Vendor vendor)
    {
        var optional = new (string Label, bool Filled)[]
        {
            ("GST number", !string.IsNullOrWhiteSpace(vendor.GstNumber)),
            ("Operating locations", !string.IsNullOrWhiteSpace(vendor.OperatingLocations)),
            ("About your business", !string.IsNullOrWhiteSpace(vendor.AdditionalInformation)),
            ("Years of experience", vendor.YearsOfExperience > 0)
        };

        var filled = optional.Count(o => o.Filled);
        var percent = (int)Math.Round((filled + RequiredFields) / (double)TotalFields * 100);

        return (percent, optional.Where(o => !o.Filled).Select(o => o.Label).ToList());
    }
}