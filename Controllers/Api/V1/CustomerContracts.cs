using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): customer accounts.

/// <summary>Before sending the SMS code: is this mobile number and email still free?</summary>
public class CustomerCheckRequest
{
    [Required(ErrorMessage = "Enter your mobile number.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }
}

/// <summary>Creating the account, after the SMS code was confirmed in the app.</summary>
public class CustomerRegisterRequest
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Your name should be 3 to 80 characters.")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Enter your mobile number.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Choose a password.")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters, including a number.")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Choose your city.")]
    [StringLength(80)]
    public string? City { get; set; }

    /// <summary>Must be true: the customer ticked "I agree to the Terms and Privacy Policy".</summary>
    public bool AcceptTerms { get; set; }

    /// <summary>
    /// Proof from Firebase that this phone received and confirmed the SMS code.
    /// Checked on the server with Firebase - the app's word alone is never trusted.
    /// </summary>
    [Required(ErrorMessage = "Verify your mobile number first.")]
    public string? FirebaseToken { get; set; }
}

/// <summary>Customer sign-in: mobile number and password.</summary>
public class CustomerLoginRequest
{
    [Required(ErrorMessage = "Enter your mobile number.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter your password.")]
    public string? Password { get; set; }
}

/// <summary>The customer details the app shows.</summary>
public record CustomerProfileDto(
    int Id,
    string FullName,
    string Phone,
    string Email,
    string City,
    DateTime JoinedAt)
{
    public static CustomerProfileDto From(Customer customer) => new(
        customer.Id,
        customer.FullName,
        customer.Phone,
        customer.Email,
        customer.City,
        customer.CreatedAt);
}

/// <summary>What sign-in and registration return.</summary>
public record CustomerAuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    CustomerProfileDto Customer,
    int LinkedRequests);

/// <summary>NEW: what the customer can change about themselves. The mobile number can't be changed here.</summary>
public class UpdateCustomerRequest
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Your name should be 3 to 80 characters.")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Choose your city.")]
    [StringLength(80)]
    public string? City { get; set; }
}

/// <summary>NEW: forgot password, step 1 - does this number have an account? (before sending an SMS)</summary>
public class CustomerResetCheckRequest
{
    [Required(ErrorMessage = "Enter your mobile number.")]
    public string? Phone { get; set; }
}

/// <summary>NEW: forgot password, step 2 - proof of the SMS code, and the new password.</summary>
public class CustomerResetPasswordRequest
{
    [Required(ErrorMessage = "Enter your mobile number.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Verify your mobile number first.")]
    public string? FirebaseToken { get; set; }

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "Use at least 8 characters, including a number.")]
    public string? NewPassword { get; set; }
}