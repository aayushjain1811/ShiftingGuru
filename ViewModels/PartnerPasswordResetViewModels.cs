using System.ComponentModel.DataAnnotations;

namespace ShiftingGuru.ViewModels.Partner;

/// <summary>NEW: /partner/forgot-password - "email me a code".</summary>
public class PartnerForgotPasswordViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email address")]
    public string? Email { get; set; }
}

/// <summary>NEW: /partner/reset-password - the code and the new password.</summary>
public class PartnerResetPasswordViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter the 6-digit code from the email.")]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "The code is 6 digits.")]
    [Display(Name = "6-digit code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters, including a number.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [Required(ErrorMessage = "Enter the new password again.")]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string? ConfirmPassword { get; set; }
}