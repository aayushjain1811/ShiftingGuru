using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels;

/// <summary>
/// A customer's quote REQUEST - their requirement, not a vendor's price.
/// This maps onto a Lead entity later; it is not itself a database model.
///
/// Fields are grouped by which service needs them. Only the group matching
/// the chosen service is shown, and only that group is validated. See
/// Validate() at the bottom.
/// </summary>
public class QuoteRequestViewModel : IValidatableObject
{
    // ---------- Step 1: service ----------

    [Required(ErrorMessage = "Please choose a service.")]
    [Display(Name = "Service")]
    public string? ServiceSlug { get; set; }

    /// <summary>Filled in by the controller from the catalog, never trusted
    /// from the browser. Used for the summary and the success page.</summary>
    [BindNever, ValidateNever]
    public string ServiceName { get; set; } = "";

    // ---------- Step 2: location ----------

    [StringLength(80)]
    [Display(Name = "Moving from")]
    public string? MovingFrom { get; set; }

    [StringLength(80)]
    [Display(Name = "Moving to")]
    public string? MovingTo { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Moving date")]
    public DateTime? MovingDate { get; set; }

    [StringLength(80)]
    [Display(Name = "Storage location")]
    public string? StorageLocation { get; set; }

    // ---------- Step 3: requirements, by service ----------

    [StringLength(40)]
    [Display(Name = "Property type")]
    public string? PropertyType { get; set; }

    [StringLength(120)]
    [Display(Name = "Approximate move size")]
    public string? MoveSize { get; set; }

    [StringLength(60)]
    [Display(Name = "Office size")]
    public string? OfficeSize { get; set; }

    [StringLength(30)]
    [Display(Name = "Approximate desks or employees")]
    public string? DeskCount { get; set; }

    [StringLength(40)]
    [Display(Name = "Vehicle type")]
    public string? VehicleType { get; set; }

    [StringLength(80)]
    [Display(Name = "Brand and model")]
    public string? VehicleModel { get; set; }

    [StringLength(20)]
    [Display(Name = "Vehicle condition")]
    public string? VehicleCondition { get; set; }

    [StringLength(80)]
    [Display(Name = "Type of goods")]
    public string? GoodsType { get; set; }

    [StringLength(120)]
    [Display(Name = "Approximate load or quantity")]
    public string? LoadDetails { get; set; }

    [StringLength(60)]
    [Display(Name = "Vehicle requirement")]
    public string? VehicleRequirement { get; set; }

    [StringLength(40)]
    [Display(Name = "Storage type")]
    public string? StorageType { get; set; }

    [StringLength(60)]
    [Display(Name = "Approximate storage size")]
    public string? StorageSize { get; set; }

    [StringLength(40)]
    [Display(Name = "Expected duration")]
    public string? StorageDuration { get; set; }

    // ---------- Step 4: contact ----------

    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(80)]
    [Display(Name = "Full name")]
    public string? CustomerName { get; set; }

    // Indian mobiles start 6-9 and run to ten digits. An optional +91 or 0
    // prefix is accepted so people can type it the way they normally would.
    [Required(ErrorMessage = "Please enter your mobile number.")]
    [RegularExpression(@"^(\+?91[\s\-]?|0)?[6-9]\d{9}$",
        ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [Display(Name = "Mobile number")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [StringLength(20)]
    [Display(Name = "Preferred contact method")]
    public string? PreferredContactMethod { get; set; }

    [StringLength(600)]
    [Display(Name = "Anything else we should know?")]
    public string? AdditionalRequirements { get; set; }

    // ---------- Display data (not posted) ----------

    [BindNever, ValidateNever]
    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    /// <summary>
    /// Conditional rules. Runs on the server after the attribute checks above,
    /// so it holds even if someone bypasses the JavaScript entirely.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(ServiceSlug)) yield break;

        if (ServiceSlug == "warehouse-storage")
        {
            if (string.IsNullOrWhiteSpace(StorageLocation))
                yield return new ValidationResult("Where do you need storage?", new[] { nameof(StorageLocation) });

            if (string.IsNullOrWhiteSpace(StorageDuration))
                yield return new ValidationResult("Roughly how long do you need it for?", new[] { nameof(StorageDuration) });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(MovingFrom))
                yield return new ValidationResult("Enter the city you are moving from.", new[] { nameof(MovingFrom) });

            if (string.IsNullOrWhiteSpace(MovingTo))
                yield return new ValidationResult("Enter the city you are moving to.", new[] { nameof(MovingTo) });
        }

        if (MovingDate is null)
        {
            yield return new ValidationResult("Pick an approximate date.", new[] { nameof(MovingDate) });
        }
        else if (MovingDate.Value.Date < DateTime.Today)
        {
            yield return new ValidationResult("The date cannot be in the past.", new[] { nameof(MovingDate) });
        }

        switch (ServiceSlug)
        {
            case "home-shifting":
                if (string.IsNullOrWhiteSpace(PropertyType))
                    yield return new ValidationResult("Select your property type.", new[] { nameof(PropertyType) });
                break;

            case "office-shifting":
                if (string.IsNullOrWhiteSpace(OfficeSize))
                    yield return new ValidationResult("Select your office size.", new[] { nameof(OfficeSize) });
                break;

            case "car-transportation":
            case "bike-transportation":
                if (string.IsNullOrWhiteSpace(VehicleType))
                    yield return new ValidationResult("Select the vehicle type.", new[] { nameof(VehicleType) });
                break;

            case "goods-transportation":
                if (string.IsNullOrWhiteSpace(GoodsType))
                    yield return new ValidationResult("Tell us what kind of goods you're sending.", new[] { nameof(GoodsType) });
                break;

            case "warehouse-storage":
                if (string.IsNullOrWhiteSpace(StorageType))
                    yield return new ValidationResult("Select the storage type.", new[] { nameof(StorageType) });
                break;
        }
    }
}