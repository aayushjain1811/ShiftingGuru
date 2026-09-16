using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

/// <summary>
/// The quote form. Deliberately has no VendorId, no LeadId binding and no
/// TotalAmount - all three are decided server-side. The lead comes from the
/// route, the vendor from the signed-in user, the total from arithmetic.
/// </summary>
public class SubmitQuoteViewModel : IValidatableObject
{
    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of zero or more.")]
    [Display(Name = "Base price")]
    public decimal BasePrice { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of zero or more.")]
    [Display(Name = "Packing charges")]
    public decimal PackingCharges { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of zero or more.")]
    [Display(Name = "Transportation charges")]
    public decimal TransportationCharges { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of zero or more.")]
    [Display(Name = "Loading / unloading")]
    public decimal LoadingUnloadingCharges { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Enter an amount of zero or more.")]
    [Display(Name = "Additional charges")]
    public decimal AdditionalCharges { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Estimated pickup date")]
    public DateOnly? EstimatedPickupDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Estimated delivery date")]
    public DateOnly? EstimatedDeliveryDate { get; set; }

    [Range(0, 365, ErrorMessage = "Enter a number of days between 0 and 365.")]
    [Display(Name = "Estimated days in transit")]
    public int? EstimatedDeliveryDays { get; set; }

    [StringLength(800, ErrorMessage = "Keep notes under 800 characters.")]
    [Display(Name = "Notes for the customer")]
    public string? VendorNotes { get; set; }

    // ---- read-only context, never bound ----
    [BindNever, ValidateNever] public Lead Lead { get; set; } = new();
    [BindNever, ValidateNever] public string? QuoteNumber { get; set; }
    [BindNever, ValidateNever] public QuoteStatus? Status { get; set; }
    [BindNever, ValidateNever] public bool IsEdit => QuoteNumber is not null;

    /// <summary>Convenience for the view; the server recomputes independently.</summary>
    public decimal Total =>
        BasePrice + PackingCharges + TransportationCharges + LoadingUnloadingCharges + AdditionalCharges;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Total <= 0)
        {
            yield return new ValidationResult(
                "The quote total must be more than zero.", new[] { nameof(BasePrice) });
        }

        if (EstimatedPickupDate.HasValue &&
            EstimatedPickupDate.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            yield return new ValidationResult(
                "The pickup date can't be in the past.", new[] { nameof(EstimatedPickupDate) });
        }

        if (EstimatedPickupDate.HasValue && EstimatedDeliveryDate.HasValue &&
            EstimatedDeliveryDate.Value < EstimatedPickupDate.Value)
        {
            yield return new ValidationResult(
                "Delivery can't be before pickup.", new[] { nameof(EstimatedDeliveryDate) });
        }
    }
}

/// <summary>Same shape as submission; the controller populates QuoteNumber.</summary>
public class EditQuoteViewModel : SubmitQuoteViewModel
{
}