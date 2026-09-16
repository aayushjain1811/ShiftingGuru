namespace ShiftingGuru.Models;

/// <summary>
/// A vendor's price offer against one assigned lead.
/// Money is decimal, never double or float - binary floating point can't
/// represent 0.10 exactly, which is unacceptable for currency.
/// </summary>
public class Quote
{
    public int Id { get; set; }

    /// <summary>Public reference, e.g. SG-Q-20260911-00001. Unique.</summary>
    public string QuoteNumber { get; set; } = "";

    public int LeadId { get; set; }
    public Lead? Lead { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // ---------- Pricing ----------
    public decimal BasePrice { get; set; }
    public decimal PackingCharges { get; set; }
    public decimal TransportationCharges { get; set; }
    public decimal LoadingUnloadingCharges { get; set; }
    public decimal AdditionalCharges { get; set; }

    /// <summary>Always recomputed on the server. Never taken from the form.</summary>
    public decimal TotalAmount { get; set; }

    // ---------- Delivery ----------
    public DateOnly? EstimatedPickupDate { get; set; }
    public DateOnly? EstimatedDeliveryDate { get; set; }
    public int? EstimatedDeliveryDays { get; set; }

    public string? VendorNotes { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Submitted;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Statuses the customer is allowed to see in their portal.</summary>
    public static readonly QuoteStatus[] CustomerVisibleStatuses =
    {
        QuoteStatus.Submitted, QuoteStatus.UnderReview, QuoteStatus.Accepted, QuoteStatus.NotSelected
    };

    /// <summary>Statuses the customer may still choose from.</summary>
    public static readonly QuoteStatus[] SelectableStatuses =
    {
        QuoteStatus.Submitted, QuoteStatus.UnderReview
    };

    /// <summary>The vendor may still change it; the admin hasn't ruled yet.</summary>
    public bool IsEditableByVendor =>
        Status is QuoteStatus.Draft or QuoteStatus.Submitted or QuoteStatus.UnderReview;

    /// <summary>Statuses that occupy the "one active quote per lead" slot.</summary>
    public static readonly QuoteStatus[] ActiveStatuses =
    {
        QuoteStatus.Draft, QuoteStatus.Submitted, QuoteStatus.UnderReview, QuoteStatus.Accepted
    };
}

public enum QuoteStatus
{
    Draft,
    Submitted,
    UnderReview,
    Accepted,

    /// <summary>The customer chose a different vendor. Not a judgement on this quote.</summary>
    NotSelected,

    Rejected,
    Expired,
    Cancelled
}

/// <summary>Currency rendering. Values are stored as plain decimals.</summary>
public static class Money
{
    private static readonly System.Globalization.CultureInfo India = new("en-IN");

    /// <summary>e.g. 28500m -> "₹28,500.00"</summary>
    public static string Format(decimal amount) => amount.ToString("C2", India);
}