using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): what the customer app sees of requests, quotes and bookings.

/// <summary>One request in the customer's lists.</summary>
public record CustomerRequestDto(
    int Id,
    string Number,
    string ServiceSlug,
    string ServiceName,
    string? MovingFrom,
    string? MovingTo,
    string? StorageLocation,
    DateOnly? MovingDate,
    string Status,
    int QuotesCount,
    DateTime CreatedAt);

/// <summary>One request with everything the customer told us.</summary>
public record CustomerRequestDetailsDto(
    int Id,
    string Number,
    string ServiceSlug,
    string ServiceName,
    string? MovingFrom,
    string? MovingTo,
    string? StorageLocation,
    DateOnly? MovingDate,
    string Status,
    int QuotesCount,
    DateTime CreatedAt,
    IReadOnlyList<LeadDetailItemDto> Details,
    string? AdditionalRequirements,
    int? SelectedQuoteId);

/// <summary>The partner behind a quote, as the customer sees them before choosing. No contact details.</summary>
public record CustomerQuoteVendorDto(
    int Id,
    string CompanyName,
    bool Verified,
    double? Rating,
    int ReviewCount,
    int YearsOfExperience);

/// <summary>One price line, e.g. "Packing: 4,500". Only lines above zero are sent.</summary>
public record QuoteLineDto(string Label, decimal Amount);

/// <summary>One quote on the customer's request.</summary>
public record CustomerQuoteDto(
    int Id,
    int RequestId,
    string QuoteNumber,
    CustomerQuoteVendorDto Vendor,
    IReadOnlyList<QuoteLineDto> Lines,
    decimal Total,
    int? DeliveryDays,
    DateOnly? PickupDate,
    DateOnly? DeliveryDate,
    string? Notes,
    string Status,
    bool Selectable,
    DateTime CreatedAt);

/// <summary>
/// A booking: a request where the customer chose a partner. The partner's
/// name and phone are included - the customer chose them.
/// </summary>
public record CustomerBookingDto(
    int Id,
    string Number,
    int QuoteId,
    string QuoteNumber,
    string ServiceSlug,
    string ServiceName,
    string? MovingFrom,
    string? MovingTo,
    string? StorageLocation,
    DateOnly? MovingDate,
    decimal Amount,
    string Stage,
    string VendorName,
    string VendorPhone,
    DateTime UpdatedAt);

/// <summary>The website's statuses, in the words the customer app uses.</summary>
public static class CustomerViews
{
    public static string RequestStatus(LeadStatus status, int visibleQuotes) => status switch
    {
        LeadStatus.Converted => "vendor-selected",
        LeadStatus.Completed => "completed",
        LeadStatus.Closed or LeadStatus.Cancelled => "cancelled",
        _ => visibleQuotes > 0 ? "quotes-available" : "finding-professionals"
    };

    public static string QuoteStatus(Models.QuoteStatus status) => status switch
    {
        Models.QuoteStatus.Accepted => "selected",
        Models.QuoteStatus.NotSelected or Models.QuoteStatus.Rejected => "rejected",
        Models.QuoteStatus.Expired or Models.QuoteStatus.Cancelled => "expired",
        _ => "submitted"
    };

    /// <summary>The website's price lines, in the website's order, without the empty ones.</summary>
    public static IReadOnlyList<QuoteLineDto> LinesOf(Quote quote) => new[]
        {
            new QuoteLineDto("Base price", quote.BasePrice),
            new QuoteLineDto("Transportation", quote.TransportationCharges),
            new QuoteLineDto("Packing", quote.PackingCharges),
            new QuoteLineDto("Loading & unloading", quote.LoadingUnloadingCharges),
            new QuoteLineDto("Additional charges", quote.AdditionalCharges)
        }
        .Where(line => line.Amount > 0)
        .ToList();
}