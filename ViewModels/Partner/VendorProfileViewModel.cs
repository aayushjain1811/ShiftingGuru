using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

/// <summary>
/// Editable business details only. There is deliberately no Id, VendorNumber,
/// IdentityUserId or Status here - the vendor is resolved from the signed-in
/// user on the server, so nothing about identity or approval can be posted.
/// </summary>
public class VendorProfileViewModel
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

    [Required(ErrorMessage = "Enter your city.")]
    [StringLength(80)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Enter your business address.")]
    [StringLength(250)]
    [Display(Name = "Business address")]
    public string? Address { get; set; }

    [StringLength(20)]
    [Display(Name = "GST number")]
    public string? GstNumber { get; set; }

    [Range(0, 80, ErrorMessage = "Enter a number between 0 and 80.")]
    [Display(Name = "Years of experience")]
    public int YearsOfExperience { get; set; }

    [Display(Name = "Services you offer")]
    public List<string> ServicesOffered { get; set; } = new();

    [StringLength(250)]
    [Display(Name = "Areas you operate in")]
    public string? OperatingLocations { get; set; }

    [StringLength(600)]
    [Display(Name = "Additional information")]
    public string? AdditionalInformation { get; set; }

    // ---- read-only context ----
    [BindNever, ValidateNever] public string VendorNumber { get; set; } = "";
    [BindNever, ValidateNever] public string Email { get; set; } = "";
    [BindNever, ValidateNever] public VendorStatus Status { get; set; }
    [BindNever, ValidateNever] public IReadOnlyList<Service> AvailableServices { get; set; } = Array.Empty<Service>();
}