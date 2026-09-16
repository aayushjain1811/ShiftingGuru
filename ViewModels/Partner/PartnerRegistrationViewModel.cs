using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

public class PartnerRegistrationViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Enter your business name.")]
    [StringLength(120)]
    [Display(Name = "Business name")]
    public string? BusinessName { get; set; }

    [Required(ErrorMessage = "Enter a contact person.")]
    [StringLength(80)]
    [Display(Name = "Contact person")]
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

    [StringLength(20)]
    [Display(Name = "GST number (optional)")]
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

    [BindNever, ValidateNever]
    public IReadOnlyList<Service> AvailableServices { get; set; } = Array.Empty<Service>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ServicesOffered.Count == 0)
        {
            yield return new ValidationResult(
                "Select at least one service you provide.", new[] { nameof(ServicesOffered) });
        }
    }
}