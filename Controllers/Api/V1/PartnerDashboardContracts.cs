using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): what the partner dashboard and lead lists return.

/// <summary>The numbers at the top of the partner dashboard.</summary>
public record PartnerStatsDto(
    int NewLeads,
    int OpenLeads,
    int PendingQuotes,
    int QuotesSubmitted,
    int AcceptedQuotes,
    int ActiveBookings,
    int CompletedJobs,
    decimal BookedValueThisMonth,
    double? Rating,
    int ReviewCount);

/// <summary>
/// One lead as it appears in a list. Deliberately has NO customer name,
/// phone or email - those belong on the lead details screen only, exactly
/// like the website's partner lead list.
/// </summary>
public record PartnerLeadSummaryDto(
    int LeadId,
    string LeadNumber,
    string ServiceSlug,
    string ServiceName,
    string? MovingFrom,
    string? MovingTo,
    string? StorageLocation,
    DateOnly? MovingDate,
    string Status,
    DateTime AssignedAt,
    string Summary);

/// <summary>
/// A one-line description of the job for lead cards, built only from the
/// service details (never contact details), e.g. "2 BHK · Flat · Fragile items".
/// </summary>
public static class PartnerLeadSummary
{
    private const int MaxParts = 3;

    public static string Build(string serviceName, params string?[] details)
    {
        var parts = details
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!.Trim())
            .Take(MaxParts)
            .ToList();

        return parts.Count == 0 ? $"New {serviceName} request" : string.Join(" · ", parts);
    }

    /// <summary>The summary for a loaded lead.</summary>
    public static string For(Lead lead) => Build(lead.ServiceName,
        lead.PropertyType, lead.MoveSize, lead.OfficeSize, lead.DeskCount,
        lead.VehicleType, lead.VehicleModel, lead.VehicleCondition,
        lead.GoodsType, lead.LoadDetails, lead.VehicleRequirement,
        lead.StorageType, lead.StorageSize, lead.StorageDuration);

    /// <summary>
    /// Every service detail that was filled in, with a readable label.
    /// The same list the website's partner and admin pages show.
    /// </summary>
    public static IReadOnlyList<LeadDetailItemDto> DetailsFor(Lead lead)
    {
        var candidates = new (string Label, string? Value)[]
        {
            ("Property type", lead.PropertyType),
            ("Approximate size", lead.MoveSize),
            ("Office size", lead.OfficeSize),
            ("Desks / employees", lead.DeskCount),
            ("Vehicle type", lead.VehicleType),
            ("Brand and model", lead.VehicleModel),
            ("Vehicle condition", lead.VehicleCondition),
            ("Type of goods", lead.GoodsType),
            ("Load details", lead.LoadDetails),
            ("Vehicle requirement", lead.VehicleRequirement),
            ("Storage type", lead.StorageType),
            ("Storage size", lead.StorageSize),
            ("Expected duration", lead.StorageDuration)
        };

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => new LeadDetailItemDto(c.Label, c.Value!.Trim()))
            .ToList();
    }
}

// ---------------------------------------------------------------------
// NEW: lead details, accept and reject
// ---------------------------------------------------------------------

/// <summary>One "label: value" line on the lead details screen.</summary>
public record LeadDetailItemDto(string Label, string Value);

/// <summary>
/// The customer, as this partner is allowed to see them.
/// Before the partner accepts: masked name ("Aayush J."), masked phone, no email.
/// The full details never leave the server until then.
/// </summary>
public record PartnerLeadCustomerDto(
    bool ContactVisible,
    string Name,
    string Phone,
    string? Email,
    string? City,
    string? PreferredContactMethod);

/// <summary>This partner's own quote on the lead, if any.</summary>
public record PartnerLeadQuoteDto(
    int QuoteId,
    string QuoteNumber,
    decimal TotalAmount,
    string Status,
    bool Editable);

/// <summary>Everything the lead details screen shows, plus which buttons to offer.</summary>
public record PartnerLeadDetailsDto(
    int LeadId,
    string LeadNumber,
    string ServiceSlug,
    string ServiceName,
    string? MovingFrom,
    string? MovingTo,
    string? StorageLocation,
    DateOnly? MovingDate,
    string Status,
    DateTime AssignedAt,
    string Summary,
    IReadOnlyList<LeadDetailItemDto> Details,
    string? AdditionalRequirements,
    PartnerLeadCustomerDto Customer,
    PartnerLeadQuoteDto? Quote,
    bool CanAccept,
    bool CanReject,
    bool CanQuote);

/// <summary>Everything the partner dashboard needs, in one request.</summary>
public record PartnerDashboardDto(
    PartnerStatsDto Stats,
    IReadOnlyList<PartnerLeadSummaryDto> RecentLeads,
    int ProfileCompletion,
    IReadOnlyList<string> MissingProfileFields);

/// <summary>
/// The website's assignment status, translated into the words the app uses.
///
///   Assigned / Viewed, no quote yet -> "new"
///   Assigned / Viewed, quote sent   -> "quoted"
///   Declined                        -> "rejected"
///   Expired                         -> "expired"
///   Completed                       -> "completed"
/// </summary>
public static class PartnerLeadStatus
{
    public static string For(AssignmentStatus status, bool hasActiveQuote) => status switch
    {
        AssignmentStatus.Declined => "rejected",
        AssignmentStatus.Expired => "expired",
        AssignmentStatus.Completed => "completed",
        _ => hasActiveQuote ? "quoted" : "new"
    };
}

// ---------------------------------------------------------------------
// NEW: quotes
// ---------------------------------------------------------------------

/// <summary>
/// What the partner app sends to submit or update a quote. The same price
/// lines the website's quote form uses; the total is always worked out on
/// the server, never taken from the app.
/// </summary>
public class PartnerQuoteRequest
{
    private const double MaxAmount = 10_000_000;   // ₹1 crore per line - a typo guard, not a business rule

    [Range(0, MaxAmount, ErrorMessage = "Enter a base price between ₹0 and ₹1 crore.")]
    public decimal BasePrice { get; set; }

    [Range(0, MaxAmount, ErrorMessage = "Enter packing charges between ₹0 and ₹1 crore.")]
    public decimal PackingCharges { get; set; }

    [Range(0, MaxAmount, ErrorMessage = "Enter transportation charges between ₹0 and ₹1 crore.")]
    public decimal TransportationCharges { get; set; }

    [Range(0, MaxAmount, ErrorMessage = "Enter loading and unloading charges between ₹0 and ₹1 crore.")]
    public decimal LoadingUnloadingCharges { get; set; }

    [Range(0, MaxAmount, ErrorMessage = "Enter additional charges between ₹0 and ₹1 crore.")]
    public decimal AdditionalCharges { get; set; }

    public DateOnly? EstimatedPickupDate { get; set; }
    public DateOnly? EstimatedDeliveryDate { get; set; }

    [Range(1, 90, ErrorMessage = "Delivery time should be between 1 and 90 days.")]
    public int? EstimatedDeliveryDays { get; set; }

    [MaxLength(800, ErrorMessage = "Keep notes under 800 characters.")]
    public string? VendorNotes { get; set; }
}

/// <summary>This partner's quote, line by line - used to fill the form when updating.</summary>
public record PartnerQuoteDto(
    int QuoteId,
    string QuoteNumber,
    int LeadId,
    decimal BasePrice,
    decimal PackingCharges,
    decimal TransportationCharges,
    decimal LoadingUnloadingCharges,
    decimal AdditionalCharges,
    decimal TotalAmount,
    DateOnly? EstimatedPickupDate,
    DateOnly? EstimatedDeliveryDate,
    int? EstimatedDeliveryDays,
    string? VendorNotes,
    string Status,
    bool Editable,
    DateTime CreatedAt)
{
    public static PartnerQuoteDto From(Quote quote) => new(
        quote.Id,
        quote.QuoteNumber,
        quote.LeadId,
        quote.BasePrice,
        quote.PackingCharges,
        quote.TransportationCharges,
        quote.LoadingUnloadingCharges,
        quote.AdditionalCharges,
        quote.TotalAmount,
        quote.EstimatedPickupDate,
        quote.EstimatedDeliveryDate,
        quote.EstimatedDeliveryDays,
        quote.VendorNotes,
        quote.Status.ToString(),
        quote.IsEditableByVendor,
        quote.CreatedAt);
}