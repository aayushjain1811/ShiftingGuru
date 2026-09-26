using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels;

/// <summary>
/// The SHORT quote form: name, mobile (verified by OTP), email, service,
/// from, to, an optional date and an optional message. Partners ask for the
/// finer details (flat size, car model, etc.) when they call the customer.
/// </summary>
public class QuoteRequestViewModel : IValidatableObject
{
    public const string StorageSlug = "warehouse-storage";

    [Required(ErrorMessage = "Please choose a service.")]
    [Display(Name = "Service")]
    public string? ServiceSlug { get; set; }

    /// <summary>Filled in by the server from the catalog, never from the form.</summary>
    [BindNever]
    public string? ServiceName { get; set; }

    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(80)]
    [Display(Name = "Full name")]
    public string? CustomerName { get; set; }

    [Required(ErrorMessage = "Please enter your mobile number.")]
    [RegularExpression(@"^(\+?91[\s\-]?|0)?[6-9]\d{9}$",
        ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [Display(Name = "Mobile number")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter the city you're moving from.")]
    [StringLength(80)]
    [Display(Name = "Moving from")]
    public string? MovingFrom { get; set; }

    /// <summary>Required for every service except storage (checked in Validate).</summary>
    [StringLength(80)]
    [Display(Name = "Moving to")]
    public string? MovingTo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Moving date")]
    public DateTime? MovingDate { get; set; }

    [StringLength(600)]
    [Display(Name = "Message")]
    public string? AdditionalRequirements { get; set; }

    /// <summary>Firebase proof that the mobile number was verified. Checked by the server.</summary>
    public string? PhoneVerificationToken { get; set; }

    [BindNever, ValidateNever]
    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ServiceSlug != StorageSlug && string.IsNullOrWhiteSpace(MovingTo))
        {
            yield return new ValidationResult(
                "Enter the city you're moving to.", new[] { nameof(MovingTo) });
        }

        if (MovingDate.HasValue && MovingDate.Value.Date < DateTime.Today.AddDays(-1))
        {
            yield return new ValidationResult(
                "Choose today or a later date.", new[] { nameof(MovingDate) });
        }
    }
}