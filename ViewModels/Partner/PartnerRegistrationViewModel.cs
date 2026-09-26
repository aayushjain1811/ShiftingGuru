using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

public class PartnerRegistrationViewModel : IValidatableObject
{
    /// <summary>Largest allowed upload. partner-join.js uses the same number.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] DocumentExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
    private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    [Required(ErrorMessage = "Enter your business name.")]
    [StringLength(120)]
    [Display(Name = "Business name")]
    public string? BusinessName { get; set; }

    // CHANGED: shown as "Full name" on the form. The property keeps its old name,
    // so the database and the rest of the app don't need to change.
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(80)]
    [Display(Name = "Full name")]
    public string? ContactPerson { get; set; }

    [Required(ErrorMessage = "Enter a mobile number.")]
    [RegularExpression(@"^(\+?91[\s\-]?|0)?[6-9]\d{9}$",
        ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [Display(Name = "Mobile number")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(100, MinimumLength = 12,
        ErrorMessage = "Use at least 12 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Enter your city.")]
    [StringLength(80)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Enter your business address.")]
    [StringLength(250)]
    [Display(Name = "Business address")]
    public string? Address { get; set; }

    // CHANGED: was optional. Small letters are allowed here on purpose;
    // the backend converts the number to capitals before saving.
    [Required(ErrorMessage = "Enter your GST number.")]
    [StringLength(15)]
    [RegularExpression(@"^[0-9]{2}[A-Za-z]{5}[0-9]{4}[A-Za-z][1-9A-Za-z][Zz][0-9A-Za-z]$",
        ErrorMessage = "Enter a valid 15-character GST number.")]
    [Display(Name = "GST number")]
    public string? GstNumber { get; set; }

    [Range(0, 80, ErrorMessage = "Enter a number between 0 and 80.")]
    [Display(Name = "Years of experience")]
    public int YearsOfExperience { get; set; }

    /// <summary>Slugs ticked on the form. Validated against the catalog server-side.</summary>
    [Display(Name = "Services you offer")]
    public List<string> ServicesOffered { get; set; } = new();

    [StringLength(250)]
    [Display(Name = "Areas you operate in")]
    public string? OperatingLocations { get; set; }

    [StringLength(600)]
    [Display(Name = "Anything else we should know?")]
    public string? AdditionalInformation { get; set; }

    // ----- NEW: documents -----

    [Required(ErrorMessage = "Upload your GST certificate.")]
    [Display(Name = "GST certificate")]
    public IFormFile? GstCertificate { get; set; }

    [Required(ErrorMessage = "Upload your PAN card.")]
    [Display(Name = "PAN card")]
    public IFormFile? PanCard { get; set; }

    [Required(ErrorMessage = "Upload the front of your Aadhaar card.")]
    [Display(Name = "Aadhaar (front)")]
    public IFormFile? AadhaarFront { get; set; }

    [Required(ErrorMessage = "Upload the back of your Aadhaar card.")]
    [Display(Name = "Aadhaar (back)")]
    public IFormFile? AadhaarBack { get; set; }

    [Required(ErrorMessage = "Upload a photo of your office.")]
    [Display(Name = "Office photo")]
    public IFormFile? OfficePhoto { get; set; }

    // ----- NEW: verification -----
    // Filled in by partner-join.js after a correct code. The backend step
    // checks these are genuine and match the email and number on the form.

    [Required(ErrorMessage = "Verify your email address with the code we send you.")]
    public string? EmailVerificationToken { get; set; }

    [Required(ErrorMessage = "Verify your mobile number with the code we send you.")]
    public string? PhoneVerificationToken { get; set; }

    // ----- NEW: registration fee -----
    // The Razorpay order id, filled in by partner-join.js after a confirmed
    // payment. The backend checks it is really paid, for this email, and unused.

    [Required(ErrorMessage = "Pay the registration fee to submit your application.")]
    [StringLength(40)]
    public string? RegistrationOrderId { get; set; }

    // ----- NEW: consent -----

    [Range(typeof(bool), "true", "true",
        ErrorMessage = "Confirm this to submit your application.")]
    public bool DocumentConsent { get; set; }

    [BindNever, ValidateNever]
    public IReadOnlyList<Service> AvailableServices { get; set; } = Array.Empty<Service>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ServicesOffered.Count == 0)
        {
            yield return new ValidationResult(
                "Select at least one service you provide.", new[] { nameof(ServicesOffered) });
        }

        // Basic size and file-type checks. The backend step adds a stronger check
        // that looks inside the file, because a file name can be faked.
        var uploads = new (IFormFile? File, string Field, string[] Allowed)[]
        {
            (GstCertificate, nameof(GstCertificate), DocumentExtensions),
            (PanCard, nameof(PanCard), DocumentExtensions),
            (AadhaarFront, nameof(AadhaarFront), DocumentExtensions),
            (AadhaarBack, nameof(AadhaarBack), DocumentExtensions),
            (OfficePhoto, nameof(OfficePhoto), PhotoExtensions)
        };

        foreach (var (file, field, allowed) in uploads)
        {
            if (file is null) continue; // [Required] already reports a missing file

            if (file.Length == 0 || file.Length > MaxFileBytes)
            {
                yield return new ValidationResult(
                    "Choose a file up to 5 MB.", new[] { field });
            }
            else if (!allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            {
                yield return new ValidationResult(
                    allowed == PhotoExtensions
                        ? "Choose a JPG, PNG or WEBP photo."
                        : "Choose a JPG, PNG, WEBP or PDF file.",
                    new[] { field });
            }
        }
    }
}