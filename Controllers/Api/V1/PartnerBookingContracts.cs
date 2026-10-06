namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): partner bookings and earnings.
//
// A booking is a lead where the customer chose this partner's quote
// (Lead.SelectedVendorId). There is no separate Bookings table - the website
// works the same way.

/// <summary>One booking in the partner's list.</summary>
public record PartnerBookingDto(
    int LeadId,
    string LeadNumber,
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
    string CustomerName,
    DateTime ConvertedAt);

/// <summary>One bar on the earnings chart.</summary>
public record EarningsMonthDto(string Month, int Year, decimal Amount);

/// <summary>
/// The partner's booked value. ShiftingGuru doesn't handle the payment between
/// customer and partner, so these are the totals of quotes customers chose -
/// the value of work won, not money paid out.
/// </summary>
public record PartnerEarningsDto(
    decimal TotalBooked,
    decimal ThisMonth,
    decimal UpcomingValue,
    int CompletedJobs,
    int TotalBookings,
    IReadOnlyList<EarningsMonthDto> Months);

/// <summary>India time. No daylight saving, so a fixed offset is exact.</summary>
public static class IndiaTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

    public static DateTime Now => DateTime.UtcNow + Offset;

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>A UTC moment shown as the India date and time.</summary>
    public static DateTime FromUtc(DateTime utc) => utc + Offset;

    /// <summary>The India date and time as UTC, for database comparisons.</summary>
    public static DateTime ToUtc(DateTime india) => DateTime.SpecifyKind(india - Offset, DateTimeKind.Utc);
}