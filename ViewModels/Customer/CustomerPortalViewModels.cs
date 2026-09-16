using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Customer;

/// <summary>One vendor's offer, shaped for customer eyes only.</summary>
public class CustomerQuoteCardViewModel
{
    public int QuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";

    public string VendorName { get; set; } = "";
    public int VendorYearsOfExperience { get; set; }
    public string VendorCity { get; set; } = "";
    public IReadOnlyList<string> VendorServices { get; set; } = Array.Empty<string>();

    public decimal BasePrice { get; set; }
    public decimal PackingCharges { get; set; }
    public decimal TransportationCharges { get; set; }
    public decimal LoadingUnloadingCharges { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal TotalAmount { get; set; }

    public DateOnly? EstimatedPickupDate { get; set; }
    public DateOnly? EstimatedDeliveryDate { get; set; }
    public int? EstimatedDeliveryDays { get; set; }

    public string? VendorNotes { get; set; }

    public QuoteStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }

    /// <summary>True when the customer can still pick this one.</summary>
    public bool IsSelectable { get; set; }
    public bool IsAccepted => Status == QuoteStatus.Accepted;

    public static CustomerQuoteCardViewModel From(Quote quote, bool leadConverted) => new()
    {
        QuoteId = quote.Id,
        QuoteNumber = quote.QuoteNumber,
        VendorName = quote.Vendor?.BusinessName ?? "Service provider",
        VendorYearsOfExperience = quote.Vendor?.YearsOfExperience ?? 0,
        VendorCity = quote.Vendor?.City ?? "",
        VendorServices = quote.Vendor?.Services.Select(s => s.ServiceName).ToList() ?? new List<string>(),
        BasePrice = quote.BasePrice,
        PackingCharges = quote.PackingCharges,
        TransportationCharges = quote.TransportationCharges,
        LoadingUnloadingCharges = quote.LoadingUnloadingCharges,
        AdditionalCharges = quote.AdditionalCharges,
        TotalAmount = quote.TotalAmount,
        EstimatedPickupDate = quote.EstimatedPickupDate,
        EstimatedDeliveryDate = quote.EstimatedDeliveryDate,
        EstimatedDeliveryDays = quote.EstimatedDeliveryDays,
        VendorNotes = quote.VendorNotes,
        Status = quote.Status,
        SubmittedAt = quote.CreatedAt,
        IsSelectable = !leadConverted && Quote.SelectableStatuses.Contains(quote.Status)
    };
}

public class CustomerRequestViewModel
{
    public Lead Lead { get; set; } = new();

    public IReadOnlyList<(string Label, string Value)> ServiceDetails { get; set; }
        = Array.Empty<(string, string)>();

    public int QuoteCount { get; set; }

    public CustomerQuoteCardViewModel? SelectedQuote { get; set; }
}

public class CustomerQuoteListViewModel
{
    public Lead Lead { get; set; } = new();
    public IReadOnlyList<CustomerQuoteCardViewModel> Quotes { get; set; }
        = Array.Empty<CustomerQuoteCardViewModel>();

    public bool LeadConverted => Lead.IsConverted;
}

public class CustomerQuoteDetailsViewModel
{
    public Lead Lead { get; set; } = new();
    public CustomerQuoteCardViewModel Quote { get; set; } = new();
}

public class CustomerVendorSelectionViewModel
{
    public Lead Lead { get; set; } = new();
    public CustomerQuoteCardViewModel Quote { get; set; } = new();
}

public class CustomerConfirmationViewModel
{
    public Lead Lead { get; set; } = new();
    public CustomerQuoteCardViewModel Quote { get; set; } = new();
}

public class RequestAccessViewModel
{
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [RegularExpression(@"^(\+?91[\s\-]?|0)?[6-9]\d{9}$",
        ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [Display(Name = "Mobile number")]
    public string? Phone { get; set; }

    /// <summary>Shown after any submission, match or not.</summary>
    public bool Submitted { get; set; }

    /// <summary>Development only: no delivery provider is configured yet.</summary>
    public string? DevelopmentLink { get; set; }
}